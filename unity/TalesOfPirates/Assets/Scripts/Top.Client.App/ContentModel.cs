using Top.Client.Models;
using Top.Logging;
using UnityEngine;

namespace Top.Client.App
{
    /// <summary>
    /// Puts one model out of the converted tree under this object. A converted
    /// file is content, not an asset imported into the project, so anything that
    /// wants a model - a mark where the player clicked, a prop, an effect - names
    /// the same path the converter wrote and this loads it.
    /// <br/>
    /// The model keeps whatever animations it carries: the store plays them as it
    /// instantiates, which is what makes a converted effect effect.
    /// </summary>
    public class ContentModel : MonoBehaviour
    {
        [SerializeField] private MapPreview _preview;
        [SerializeField] private string _path;

        /// <summary>Which way the model is turned here, for a model exported facing somewhere else.</summary>
        [SerializeField] private Vector3 _rotation;

        [SerializeField] private float _scale = 1f;

        /// <summary>Whether the placeholder the scene put here is hidden once the model is up.</summary>
        [SerializeField] private bool _hidePlaceholder = true;

        private ModelStore _store;
        private ModelInstance _instance;

        /// <summary>
        /// The path in the converted tree to load, for whoever builds this object
        /// rather than wiring it in the inspector. Set it before the frame is out:
        /// the load happens in Start.
        /// </summary>
        public string Path
        {
            get { return _path; }
            set { _path = value; }
        }

        /// <summary>
        /// Which way the model is turned, for whoever builds this object rather
        /// than wiring it in the inspector.
        /// </summary>
        public Vector3 Rotation
        {
            get { return _rotation; }
            set { _rotation = value; }
        }

        /// <summary>How big the model is drawn, for whoever builds this object.</summary>
        public float Scale
        {
            get { return _scale; }
            set { _scale = value; }
        }

        private async void Start()
        {
            var preview = _preview != null ? _preview : FindAnyObjectByType<MapPreview>();

            if (preview == null || preview.Content == null)
            {
                Log.Error($"no map preview to load '{_path}' from");

                return;
            }

            if (string.IsNullOrEmpty(_path))
            {
                return;
            }

            HidePlaceholder();

            _store = new ModelStore(preview.Content, preview.ModelShader);

            var holder = new GameObject("Model");

            holder.transform.SetParent(transform, worldPositionStays: false);
            holder.transform.localRotation = Quaternion.Euler(_rotation);
            holder.transform.localScale = Vector3.one * _scale;

            try
            {
                _instance = await _store.Instantiate(_path, holder.transform);
            }
            catch (System.Exception exception)
            {
                Log.Error($"could not load '{_path}'", exception);
            }
        }

        private void OnDestroy()
        {
            _instance?.Dispose();
            _instance = null;
        }

        private void HidePlaceholder()
        {
            if (!_hidePlaceholder)
            {
                return;
            }

            var placeholder = GetComponent<MeshRenderer>();

            if (placeholder != null)
            {
                placeholder.enabled = false;
            }
        }
    }
}
