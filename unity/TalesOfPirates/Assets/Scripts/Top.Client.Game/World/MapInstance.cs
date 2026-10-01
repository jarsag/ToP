using System;
using Top.Client.Core;
using Top.Client.Game.World.SceneObjects;
using Top.Contracts.Tables.World;
using UnityEngine;

namespace Top.Client.Game.World
{
    public class MapInstance : IDisposable
    {
        private readonly ChunkStreamer _chunkStreamer;
        private readonly MapData _mapData;

        private Transform _root;

        public MapInstance(MapEntry mapEntry, MapData mapData, Transform parent, Material terrainMaterial,
            Material waterMaterial, ISceneObjectFactory sceneObjectFactory, float streamingRadius)
        {
            MapEntry = mapEntry;
            _mapData = mapData;

            _root = CreateGroup("Map", parent);

            _chunkStreamer = new ChunkStreamer(mapData, streamingRadius,
                new TerrainLoader(mapData, CreateGroup("Terrain", _root), terrainMaterial),
                new WaterLoader(mapData, CreateGroup("Water", _root), waterMaterial),
                new SceneObjectLoader(mapData, CreateGroup("SceneObjects", _root), sceneObjectFactory)
            );
        }

        public MapEntry MapEntry { get; }

        /// <summary>
        /// The map's terrain and attribute data, for anything that needs to
        /// stand on it rather than only look at it.
        /// </summary>
        public MapData Data => _mapData;

        public void SetCenter(Vector3 worldPosition)
        {
            _chunkStreamer.SetCenter(MapSpace.ToMap(worldPosition));
        }

        public void Dispose()
        {
            _chunkStreamer.Dispose();

            if (_root != null)
            {
                UnityObjects.Destroy(_root.gameObject);
                _root = null;
            }
        }

        private static Transform CreateGroup(string groupName, Transform parent)
        {
            var group = new GameObject(groupName);

            group.transform.SetParent(parent, worldPositionStays: false);

            return group.transform;
        }
    }
}
