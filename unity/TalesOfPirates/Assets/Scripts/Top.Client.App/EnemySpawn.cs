using System.Collections.Generic;
using Top.Client.Game.World;
using Top.Logging;
using UnityEngine;

namespace Top.Client.App
{
    /// <summary>
    /// Stands bodies to practise on, in one place. <br/>
    /// Hung on a zone marked as an enemy spawn: the zone says where, and this says what - which body,
    /// how many, and what clip it stands in. Hung rather than folded into the zone because the two are
    /// different things: a zone is ground a designer drew, and this needs the converted tree to put
    /// anything on it, which ground knows nothing about.
    /// </summary>
    public class EnemySpawn : MonoBehaviour
    {
        [Header("What stands here")]
        [SerializeField] private string _enemy = "dummy";

        [Tooltip("Model of the client's the body wears. The clips come inside it.")]
        [SerializeField] private string _model = "0535";

        [Tooltip("Clip it stands in. Empty takes the first waiting clip the model carries.")]
        [SerializeField] private string _idle = string.Empty;

        [Tooltip("How many bodies stand here.")]
        [SerializeField] private int _count = 1;

        [SerializeField] private float _scale = 1f;

        /// <summary>The zone this fills, when it is not standing in one itself.</summary>
        [Tooltip("Zone to fill. Empty takes the zone on this object, if there is one.")]
        [SerializeField] private Zone _zone;

        private readonly List<Enemy> _spawned = new List<Enemy>();

        private bool _filled;

        /// <summary>Whether a key can be pressed to hit what stands here, for trying a scene out.</summary>
        [Header("Trying it out")]
        [Tooltip("Key that hits what stands here, so that a number can be seen without a skill yet.")]
        [SerializeField] private bool _testKey = true;

        /// <summary>The kind of body that stands here, and how many.</summary>
        public string Kind => _enemy;

        public int Count => _count;

        /// <summary>What was put here, for anything that wants to hit it.</summary>
        public IReadOnlyList<Enemy> Spawned => _spawned;

        private void Start()
        {
            Fill();
        }

        /// <summary>
        /// Hits one of the bodies here, as a skill would. There is nothing to hit with yet, so a key
        /// does it - which is how a number is seen before there is a skill to make one.
        /// </summary>
        private void Update()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;

            if (!_testKey || keyboard == null || keyboard.spaceKey == null)
            {
                return;
            }

            if (!keyboard.spaceKey.wasPressedThisFrame || _spawned.Count == 0)
            {
                return;
            }

            var critical = Random.value < 0.25f;

            _spawned[Random.Range(0, _spawned.Count)].Strike(Random.Range(1, 999), critical);
        }

        /// <summary>
        /// Puts the bodies down where this stands, on the ground under it. <br/>
        /// The place is read off the transform itself, so that moving the object in the scene moves what
        /// stands on it: a spawner put down somewhere and its bodies put down somewhere else would be two
        /// things to keep in step by hand.
        /// </summary>
        public async System.Threading.Tasks.Task Fill()
        {
            if (_filled)
            {
                return;
            }

            var preview = MapPreview.InScene();

            if (preview == null)
            {
                Log.Warning($"'{name}' has no map preview to take the bodies' models from");

                return;
            }

            _filled = true;

            var count = Mathf.Max(_count, 0);

            Log.Info($"'{name}' stands {count} {_enemy}(s) of model '{_model}' at {transform.position}");

            for (var i = 0; i < count; i++)
            {
                var at = Ground(preview, transform.position, i, count, out var facing);

                var body = Enemy.Spawn(preview, transform.root, at, facing, _model, _idle, _scale);

                _spawned.Add(body);
            }

            await System.Threading.Tasks.Task.CompletedTask;
        }

        /// <summary>
        /// Where the i-th of a group stands: on the ground under the place this is, and spread a little
        /// about it when there are several. One body stands exactly where the object is, where a group of
        /// one would look pushed aside if it did not.
        /// </summary>
        private static Vector3 Ground(MapPreview preview, Vector3 where, int i, int count, out float facing)
        {
            var at = where;

            if (count > 1)
            {
                var turn = (Mathf.PI * 2f * i) / count;

                // A short reach about the middle, so that a handful of bodies read as standing together
                // rather than as a ring drawn on the ground.
                at += new Vector3(Mathf.Cos(turn), 0f, Mathf.Sin(turn)) * (0.8f * count * 0.5f);
            }

            facing = Random.Range(0f, 360f);

            var map = preview.Data;

            if (map == null)
            {
                return at;
            }

            // On the ground rather than at whatever height the object sits at: the map owns the height,
            // and a body put down by hand is put down on the surface.
            var point = MapSpace.ToMap(at);

            return MapSpace.ToWorld(point.x, point.y, map.SurfaceAt(point.x, point.y));
        }
    }
}
