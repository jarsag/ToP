using System;
using System.Threading.Tasks;
using Top.Client.Models;
using Top.Logging;
using UnityEngine;

namespace Top.Client.App
{
    /// <summary>
    /// Something to hit: a body with a model of its own that stands where it is put and does nothing
    /// but breathe. <br/>
    /// A mannequin rather than a monster - it has no health to lose and no mind of its own, and hitting
    /// it does no more than say so out loud. It is what a skill is tried against before there is
    /// anything that can be killed.
    /// </summary>
    public class Enemy : MonoBehaviour
    {
        [SerializeField] private string _model = "0535";

        /// <summary>Which clip it stands in. Empty means the first the model has that is a waiting one.</summary>
        [SerializeField] private string _idle = string.Empty;

        [SerializeField] private float _scale = 1f;

        private ModelStore _store;

        private ModelInstance _instance;

        private Animation _acting;

        private string _playing;

        /// <summary>The model it wears, and the name it is known by.</summary>
        public string Model => _model;

        /// <summary>What it is asked for when something hits it, and the amount that was hit for.</summary>
        public event Action<int> Struck;

        /// <summary>
        /// Puts a body of this model in the world under a preview, which is what holds the converted
        /// tree and the shader its models are drawn with.
        /// </summary>
        public static Enemy Spawn(MapPreview content, Transform parent, Vector3 at, float facing,
            string model, string idle = null, float scale = 1f)
        {
            var body = new GameObject($"Enemy {model}");

            body.transform.SetParent(parent, worldPositionStays: false);
            body.transform.position = at;
            body.transform.rotation = Quaternion.Euler(0f, facing, 0f);

            var enemy = body.AddComponent<Enemy>();

            enemy._model = model;
            enemy._idle = idle ?? string.Empty;
            enemy._scale = scale;
            enemy._store = new ModelStore(content.Content, content.ModelShader);

            return enemy;
        }

        private void OnDestroy()
        {
            _instance?.Dispose();
        }

        /// <summary>Says it was hit, which is all a mannequin can do about it.</summary>
        public void Strike(int amount)
        {
            Struck?.Invoke(amount);
        }

        /// <summary>
        /// Takes the model on and stands in a waiting clip. <br/>
        /// The clips are inside the model - a client model carries its own animation - so the clip is
        /// named after the model it came in, which is why choosing a model is choosing its animations.
        /// </summary>
        private async void Start()
        {
            await Build();
        }

        private async Task Build()
        {
            try
            {
                _instance = await _store.Instantiate($"models/character/{_model}.glb", transform);
            }
            catch (Exception exception)
            {
                Log.Error($"could not put an enemy of model '{_model}' in the world", exception);

                return;
            }

            if (_instance == null || _instance.Root == null)
            {
                return;
            }

            transform.localScale = Vector3.one * _scale;

            _acting = _instance.Root.GetComponentInChildren<Animation>();

            if (_acting == null)
            {
                Log.Warning($"model '{_model}' came with no clips, so it will stand still");

                return;
            }

            // A waiting clip rather than one named by hand where none was named: every model of the
            // client's names its own actions the same way, so the first waiting one is a safe guess and
            // an empty setting is a setting nobody has to fill in.
            var clip = string.IsNullOrEmpty(_idle) ? Waiting() : _idle;

            if (string.IsNullOrEmpty(clip))
            {
                Log.Warning($"model '{_model}' has no waiting clip to stand in");

                return;
            }

            if (_acting.GetClip(clip) == null)
            {
                Log.Warning($"model '{_model}' has no clip called '{clip}'");

                return;
            }

            _acting.clip = _acting.GetClip(clip);
            _acting.wrapMode = WrapMode.Loop;
            _acting.Play();

            _playing = clip;
        }

        /// <summary>Which clip it is standing in, for anything that wants to know.</summary>
        public string Playing => _playing;

        /// <summary>The first clip of the model's own that waits, which is how the client names one.</summary>
        private string Waiting()
        {
            foreach (AnimationState state in _acting)
            {
                if (state.clip != null && state.clip.name.EndsWith("_waiting", StringComparison.Ordinal))
                {
                    return state.clip.name;
                }
            }

            return null;
        }
    }
}
