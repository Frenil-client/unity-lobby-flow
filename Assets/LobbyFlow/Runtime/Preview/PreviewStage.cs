using UnityEngine;

namespace LobbyFlow.Preview
{
    /// <summary>
    /// 미리보기 전용 카메라와 렌더 텍스처. 화면 밖 먼 위치에 두어 씬의 주 카메라에 잡히지 않게 한다.
    /// 만든 화면이 소유하고 파괴하며, 파괴될 때 렌더 텍스처도 함께 반환한다.
    /// </summary>
    public sealed class PreviewStage : MonoBehaviour
    {
        private const float RotateSpeed = 30f;

        private Camera _camera;
        private RenderTexture _texture;

        public Transform Anchor { get; private set; }
        public RenderTexture Texture => _texture;

        public static PreviewStage Create(string name, Vector3 position, int textureSize, Color background)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            var stage = go.AddComponent<PreviewStage>();

            var anchor = new GameObject("Anchor").transform;
            anchor.SetParent(go.transform, false);
            stage.Anchor = anchor;

            stage._texture = new RenderTexture(textureSize, textureSize, 24) { name = $"{name}_RT" };

            var cameraGo = new GameObject("PreviewCamera");
            cameraGo.transform.SetParent(go.transform, false);
            cameraGo.transform.localPosition = new Vector3(0f, 1.1f, -4f);
            cameraGo.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);

            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.fieldOfView = 35f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 20f;
            camera.targetTexture = stage._texture;
            stage._camera = camera;

            return stage;
        }

        private void Update()
        {
            if (Anchor != null)
                Anchor.Rotate(0f, RotateSpeed * Time.deltaTime, 0f, Space.Self);
        }

        private void OnDestroy()
        {
            if (_camera != null)
                _camera.targetTexture = null;

            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
                _texture = null;
            }
        }
    }
}
