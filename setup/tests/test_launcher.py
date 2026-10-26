"""No Unity, SDK downloads, game content, signing or device access in fixtures."""
import argparse
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

HERE = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(HERE))
import launcher
import release_snapshot
sys.path.insert(0, str(HERE / 'stages'))
import mobile_banks

class LauncherTests(unittest.TestCase):
    def test_failure_retry_and_no_false_completion(self):
        with tempfile.TemporaryDirectory() as d:
            state = Path(d) / 'state.json'
            steps = [('a', ['a']), ('b', ['b']), ('c', ['c'])]
            calls = []
            def fail(name, cmd, env):
                calls.append(name)
                if name == 'b':
                    raise RuntimeError('intentional failure')
            with self.assertRaises(RuntimeError):
                launcher.run_steps(steps, state, {}, runner=fail)
            self.assertEqual(list(json.loads(state.read_text())), ['a'])
            launcher.run_steps(steps, state, {}, runner=lambda n, c, e: calls.append(n))
            self.assertEqual(calls, ['a', 'b', 'b', 'c'])
            calls.clear()
            launcher.run_steps(steps, state, {}, start='b', until='b', runner=lambda n,c,e: calls.append(n))
            self.assertEqual(calls, ['b'])
            self.assertEqual(set(json.loads(state.read_text())), {'a','b'})

    def test_real_subprocess_failure_then_resume(self):
        with tempfile.TemporaryDirectory() as d:
            state = Path(d) / 'state.json'
            steps = [('a', [sys.executable, '-c', 'pass']), ('b', [sys.executable, '-c', 'raise SystemExit(7)'])]
            with patch.object(launcher, 'idle'):
                with self.assertRaises(RuntimeError):
                    launcher.run_steps(steps, state, {})
                self.assertEqual(set(json.loads(state.read_text())), {'a'})
                steps[1] = ('b', [sys.executable, '-c', 'pass'])
                launcher.run_steps(steps, state, {})
            self.assertEqual(set(json.loads(state.read_text())), {'a', 'b'})
            self.assertEqual(len(list((Path(d)/'launcher-logs').glob('*.log'))), 3)

    def test_bad_resume_range_does_not_change_receipts(self):
        with tempfile.TemporaryDirectory() as d:
            p=Path(d)/'state.json'
            launcher.atomic(p, {'a':{'done':'now'}, 'b':{'done':'now'}})
            before=p.read_bytes()
            with self.assertRaises(RuntimeError):
                launcher.run_steps([('a',[]),('b',[])],p,{},start='b',until='a')
            self.assertEqual(p.read_bytes(),before)

    def test_lock(self):
        with tempfile.TemporaryDirectory() as d:
            with launcher.lock(Path(d)):
                with self.assertRaises(RuntimeError):
                    with launcher.lock(Path(d)):
                        pass
            with launcher.lock(Path(d)):
                pass

    def test_plan_uses_real_stages_without_bee_sign_or_install(self):
        for target in ['android','ios','both']:
            plan = launcher.plan({'target':target}, Path('/python'))
            self.assertFalse(any('offline' in n for n,c in plan))
            for name, cmd in plan:
                self.assertTrue(Path(cmd[2]).is_file())
                self.assertFalse(any(x in cmd for x in ['keytool','codesign','install','push']))
                if name.startswith(('android.','ios.')):
                    self.assertIn('--source-workflow',cmd)
            self.assertEqual(len(plan),len({n for n,c in plan}))

    def test_signing_environment_removed(self):
        with patch.dict('os.environ', {'BDSP_IOS_TEAM_ID':'test-team','BDSP_SETUP_CONFIG':'wrong','BDSP_ANDROID_TEXTURES':'native'}):
            env=launcher.environment({'target':'both'})
        self.assertNotIn('BDSP_IOS_TEAM_ID',env)
        self.assertNotIn('BDSP_ANDROID_TEXTURES',env)
        self.assertEqual(env['BDSP_NO_SIGN'],'1')
        self.assertNotEqual(env['BDSP_SETUP_CONFIG'],'wrong')

    def test_install_requires_explicit_opt_in_target_device_artifact(self):
        with tempfile.TemporaryDirectory() as d:
            apk=Path(d)/'signed.apk';apk.write_bytes(b'fixture')
            args=argparse.Namespace(allow_install=False,device='test-id',artifact=str(apk),target='android')
            with self.assertRaises(RuntimeError): launcher.install_command(args)
            args.allow_install=True
            cmd=launcher.install_command(args)
            self.assertEqual(cmd[1:],['-s','test-id','install','-r',str(apk.resolve())])
            args.device=None
            with self.assertRaises(RuntimeError): launcher.install_command(args)
            args.device='test-id';args.target='ios'
            with self.assertRaises(RuntimeError): launcher.install_command(args)
            app=Path(d)/'signed.app';app.mkdir();(app/'embedded.mobileprovision').write_text('fixture')
            args.artifact=str(app)
            self.assertEqual(launcher.install_command(args)[:5],['xcrun','devicectl','device','install','app'])
            # Commands inspected only, never executed.

    def test_banks_resume_conflict_and_interruption(self):
        with tempfile.TemporaryDirectory() as d:
            base=Path(d);(base/'Windows').mkdir()
            (base/'Windows/Init.bnk').write_bytes(b'fixture')
            (base/'Windows/other.wem').write_bytes(b'fixture-2')
            mobile_banks.prepare(base);mobile_banks.prepare(base)
            self.assertEqual((base/'Mac/Init.bnk').read_bytes(),b'fixture')
            (base/'Mac/Init.bnk').write_bytes(b'conflict')
            (base/'Windows/new.wem').write_bytes(b'new')
            with self.assertRaises(RuntimeError): mobile_banks.prepare(base)
            self.assertFalse((base/'Mac/new.wem').exists())

    def test_no_execution_on_missing_install_opt_in(self):
        p=subprocess.run([sys.executable,'-I',str(HERE/'launcher.py'),'install','--target','android'],capture_output=True,text=True)
        self.assertEqual(p.returncode,1)
        self.assertIn('--allow-install',p.stderr)

    def test_distribution_name_normalization(self):
        with patch.object(launcher.subprocess, 'run', return_value=subprocess.CompletedProcess([], 0, '{"fmod-toolkit":"0.1.3"}', '')):
            with patch.object(Path, 'read_text', return_value='fmod_toolkit==0.1.3\n'):
                self.assertEqual(launcher.pins(Path('/python')), [])

    def test_snapshot_rejects_unsafe_paths(self):
        for path in ['../outside','/absolute','handoff/report.md','Library/cache','asset.dll']:
            with self.assertRaises(ValueError): release_snapshot.inspect(Path('/tmp'),[path])

if __name__=='__main__':unittest.main()
