import argparse
import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import mobile_data
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'stages'))
import android_package


class ReleaseToolsTests(unittest.TestCase):
    def test_unsigned_copy_preserves_original_and_omits_credentials(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp); src = root / 'src'; src.mkdir()
            (src / 'settings.gradle').write_text("include ':launcher'\n")
            original = "android {\n signingConfigs {\n release {\n storePassword 'fixture-only'\n }\n }\n buildTypes {\n release {\n signingConfig signingConfigs.release\n }\n }\n}\n"
            (src / 'build.gradle').write_text(original)
            (src / 'gradle.properties').write_text('unityProjectPath=/old\nunity.projectPath=/old\n')
            (src / 'local.properties').write_text('sdk.dir=/old\n')
            dest, init = android_package.prepare(src, root / 'output', root / 'sdk')
            self.assertEqual((src / 'build.gradle').read_text(), original)
            self.assertNotIn('fixture-only', (dest / 'build.gradle').read_text())
            self.assertNotIn('/old', (dest / 'gradle.properties').read_text())
            self.assertIn('signingConfig = null', init.read_text())
            with self.assertRaises(RuntimeError):
                android_package.prepare(src, root / 'output', root / 'sdk')

    def test_gradle_outputs_skipped_but_il2cpp_toolchain_copied(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp); src = root / 'src'
            tool = src / 'unityLibrary/src/main/Il2CppOutputProject/IL2CPP/build/deploy/il2cpp'
            tool.parent.mkdir(parents=True); tool.write_bytes(b'tool'); tool.chmod(0o755)
            (src / 'settings.gradle').write_text("include ':unityLibrary'\n")
            (src / 'build.gradle').write_text('// root\n')
            (src / 'unityLibrary/build.gradle').write_text('// module\n')
            for out in (src / 'build/x', src / 'unityLibrary/build/y'):
                out.parent.mkdir(parents=True, exist_ok=True); out.write_text('gradle output')
            dest, _ = android_package.prepare(src, root / 'output', root / 'sdk')
            copied = dest / 'unityLibrary/src/main/Il2CppOutputProject/IL2CPP/build/deploy/il2cpp'
            self.assertEqual(copied.read_bytes(), b'tool')
            self.assertTrue(copied.stat().st_mode & 0o111)
            self.assertFalse((dest / 'build').exists())
            self.assertFalse((dest / 'unityLibrary/build').exists())

    def test_unknown_signing_and_symlinks_fail_closed(self):
        with self.assertRaises(RuntimeError):
            android_package.unsigned_gradle('android.signingConfig = customSigning()')
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp);src=root/'src';src.mkdir()
            (src/'settings.gradle').write_text('')
            (src/'outside').symlink_to('/tmp')
            with self.assertRaises(RuntimeError):
                android_package.prepare(src,root/'out',root/'sdk')
            self.assertFalse((root/'out').exists())

    def test_data_preparation_hashes_and_conflict_rejection(self):
        with tempfile.TemporaryDirectory() as temp:
            r=Path(temp);dump=r/'dump';built=r/'built';out=r/'out'
            sa=dump/'romfs/Data/StreamingAssets'
            for p in [sa/'Movie/a.webm',sa/'Audio/GeneratedSoundBanks/Switch/Init.bnk',
                      built/'StreamingAssets/AssetAssistant/Dpr.bin']:
                p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b'fixture')
            (built/'BUILD.txt').write_text('android player fixture\n')
            with patch.object(mobile_data.check,'FINGERPRINTS',[]):
                with self.assertRaises(RuntimeError):mobile_data.prepare(dump,built,'android',out,False)
                mobile_data.prepare(dump,built,'android',out,True)
                mobile_data.verify(out,'android')
                with self.assertRaises(RuntimeError):mobile_data.prepare(dump,built,'android',out,True)
            with self.assertRaises(RuntimeError):mobile_data.verify(out,'ios')
            (out/'StreamingAssets/Movie/a.webm').write_bytes(b'changed')
            with self.assertRaises(RuntimeError):mobile_data.verify(out,'android')

    def test_device_commands_require_opt_in_and_never_launch(self):
        a=argparse.Namespace(allow_install=False,device='fixture',bundle_id='com.opendpr.bdsp',
                             action='push',data=None,target='android',artifact=None)
        with self.assertRaises(RuntimeError):mobile_data.device_commands(a)
        a.allow_install=True;a.data='/fixture'
        with patch.object(mobile_data,'verify'):
            commands=mobile_data.device_commands(a)
            self.assertEqual(len(commands),3)
            self.assertTrue(all(c[1:3]==['-s','fixture'] for c in commands))
            self.assertFalse(any('rm' in c or 'am' in c for c in commands))
            a.bundle_id='com.bad;touch /tmp/unwanted'
            with self.assertRaises(RuntimeError):mobile_data.device_commands(a)


if __name__ == '__main__':
    unittest.main()
