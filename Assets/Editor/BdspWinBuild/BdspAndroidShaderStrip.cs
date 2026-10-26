// Android only (editor, build time): drops the PROCEDURAL_INSTANCING_ON variants of FxSystem/Particle.
// Those variants read per-particle data from a StructuredBuffer in the vertex stage. GLES 3.0 (Pixel / Mali) allows 0 vertex-stage
// storage blocks, so every such variant fails to link ("number of vertex shader storage blocks (1) is greater than the maximum
// number allowed (0)"). Without them the particle system falls back to ordinary (non-procedural) rendering. Other build targets
// (macOS / Metal) are not touched: the check below is the active build target, and the shader file itself is unchanged.
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace BdspWinBuild
{
	public class AndroidShaderStrip : IPreprocessShaders
	{
		public int callbackOrder { get { return 0; } }

		public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
		{
			if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android || shader == null || shader.name != "FxSystem/Particle")
				return;
			ShaderKeyword procedural = new ShaderKeyword("PROCEDURAL_INSTANCING_ON");
			for (int i = data.Count - 1; i >= 0; i--)
			{
				if (data[i].shaderKeywordSet.IsEnabled(procedural))
					data.RemoveAt(i);
			}
		}
	}
}
