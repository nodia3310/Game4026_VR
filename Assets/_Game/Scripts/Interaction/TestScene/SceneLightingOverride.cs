using UnityEngine;
using UnityEngine.Rendering;

namespace Game4026.Interaction.TestScene
{
    /// <summary>Temporarily uses a test-room pipeline. Does not edit quality assets.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-500)]
    public sealed class SceneLightingOverride : MonoBehaviour
    {
        [SerializeField] private RenderPipelineAsset testPipeline;
        private RenderPipelineAsset previousPipeline;
        private bool applied;

        private void OnEnable()
        {
            if (!testPipeline) return;
            previousPipeline = QualitySettings.renderPipeline;
            QualitySettings.renderPipeline = testPipeline;
            applied = true;
        }

        private void OnDisable()
        {
            // Do not overwrite a later, explicit pipeline change from another system.
            if (applied && QualitySettings.renderPipeline == testPipeline)
                QualitySettings.renderPipeline = previousPipeline;
            applied = false;
        }
    }
}
