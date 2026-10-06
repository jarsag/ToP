using System.Collections.Generic;
using Top.Client.Game.World;
using Top.Logging;
using UnityEngine;

namespace Top.Client.App
{
    /// <summary>
    /// Where bodies to hit are put: a marked place that fills itself with as many as it is told to.
    /// <br/>
    /// The same idea as the hero's own spawn points - a place a body is set down on - with the body
    /// being something to practise on rather than the player. It stands beside a Zone marked as an
    /// enemy spawn rather than on it, so that the marking of the ground and the filling of it stay two
    /// separate things: a zone is geometry a designer drew, and this is what happens to be standing on
    /// it. One zone may have several of these, and none.
    /// </summary>
    public class EnemySpawn : MonoBehaviour
    {
        [Header("What is put here")]
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

        /// <summary>Whether a key can be pressed to hit what stands here, for trying a scene out.</summary>
        [Header("Trying it out")]
        [Tooltip("Key that hits what stands here, so that a number can be seen without a skill yet.")]
        [SerializeField] private bool _testKey = true;

        /// <summary>What was put here, for anything that wants to hit it.</summary>
        public IReadOnlyList<Enemy> Spawned => _spawned;

        private async void Start()
        {
            await Fill();
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
        /// Puts the bodies down, spread evenly about the middle of the zone so that several do not
        /// stand in one another.
        /// </summary>
        public async System.Threading.Tasks.Task Fill()
        {
            var preview = MapPreview.InScene();

            if (preview == null)
            {
                Log.Warning("there is no map preview to take the enemies' models from");

                return;
            }

            var zone = _zone != null ? _zone : GetComponent<Zone>();
            var where = zone != null ? zone.transform.position : transform.position;
            var reach = zone != null ? zone.Radius : 0f;

            var count = Mathf.Max(_count, 0);

            for (var i = 0; i < count; i++)
            {
                var at = Ground(preview, where, reach, i, count, out var facing);

                var enemy = Enemy.Spawn(preview, transform.root, at, facing, _model, _idle, _scale);

                _spawned.Add(enemy);

                Log.Info($"an enemy of model '{_model}' stands at {at}");
            }

            await System.Threading.Tasks.Task.CompletedTask;
        }

        /// <summary>
        /// Where the i-th of a group stands: on the ground, spread round the middle of the zone. One
        /// body stands in the middle rather than on the rim, where a group of one would look put there
        /// by mistake.
        /// </summary>
        private static Vector3 Ground(MapPreview preview, Vector3 where, float reach, int i, int count,
            out float facing)
        {
            var at = where;

            if (count > 1)
            {
                var turn = (Mathf.PI * 2f * i) / count;

                // Two thirds of the way out rather than on the rim, so a body's own width does not
                // hang over the edge of the place it was put.
                at += new Vector3(Mathf.Cos(turn), 0f, Mathf.Sin(turn)) * reach * 0.66f;
            }

            facing = Random.Range(0f, 360f);

            var map = preview.Data;

            if (map == null)
            {
                return at;
            }

            var point = MapSpace.ToMap(at);
            var height = map.SurfaceAt(point.x, point.y);

            return MapSpace.ToWorld(point.x, point.y, height);
        }
    }
}
