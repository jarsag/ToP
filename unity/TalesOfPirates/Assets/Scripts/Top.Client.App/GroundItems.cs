using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Top.Logging;

namespace Top.Client.App
{
    /// <summary>
    /// What lies on the ground: things taken out of the bag and let go of outside it. They
    /// live in memory for one run of the game and nothing about them outlives it, and each
    /// is thrown from the hero to the spot it was dropped on. The right button takes one
    /// back, and alt with A gathers everything within reach of the hero.
    /// </summary>
    public class GroundItems : MonoBehaviour
    {
        /// <summary>How long a thing is in the air on its way to the ground.</summary>
        [SerializeField] private float _flight = 0.5f;

        /// <summary>How high it rises on the way, in metres.</summary>
        [SerializeField] private float _arc = 1.5f;

        /// <summary>How wide the bag a dropped item is drawn as.</summary>
        [SerializeField] private float _size = 0.45f;

        /// <summary>How fast what has landed turns, in degrees a second.</summary>
        [SerializeField] private float _spin = 60f;

        /// <summary>How far it rises and falls while it waits, in metres.</summary>
        [SerializeField] private float _hover = 0.12f;

        /// <summary>How big the model of a thing on the ground is drawn, from the converted tree.</summary>
        [SerializeField] private float _modelScale = 1f;

        /// <summary>Which way that model is turned, for one exported facing somewhere else.</summary>
        [SerializeField] private Vector3 _modelRotation = Vector3.zero;

        /// <summary>How far alt and A reaches, in metres, measured from the hero.</summary>
        [SerializeField] private float _gather = 3f;

        /// <summary>How close the hero has to be to a thing to take it, in metres.</summary>
        [SerializeField] private float _pick = 1.5f;

        /// <summary>How far a ray may look for a bag to begin with, in metres.</summary>
        [SerializeField] private float _click = 40f;

        [SerializeField] private bool _debug = true;

        /// <summary>Where a thing picked up off the ground goes, which is the bag it came out of.</summary>
        public Action<int> Picked;

        /// <summary>
        /// Whether the pointer is over something lying on the ground. It is what the hero
        /// has to know before he walks: a click that lands on a thing picks it up instead.
        /// The cursor answers to the same thing, which is how the client showed which of
        /// the two a click was about to do.
        /// </summary>
        public static bool UnderPointer { get; private set; }

        private readonly List<Lying> _lying = new List<Lying>();

        /// <summary>One thing on the ground, and the throw it is still making.</summary>
        private class Lying
        {
            public GameObject Bag;

            public int ItemId;

            public Vector3 From;

            public Vector3 To;

            public float Left;

            public float Flight;
        }

        /// <summary>How many things are on the ground, which is what the log reports.</summary>
        public int Count => _lying.Count;

        /// <summary>
        /// Lets an item go at a place on the ground, thrown from where the hero stands: the
        /// spot is settled at once and only the bag has to travel there.
        /// </summary>
        public void Drop(int itemId, string model, Vector3 from, Vector3 spot)
        {
            var bag = GameObject.CreatePrimitive(PrimitiveType.Sphere);

            bag.name = $"Ground item {itemId}";

            bag.transform.SetParent(transform, worldPositionStays: true);
            bag.transform.position = from;
            bag.transform.localScale = new Vector3(_size, _size * 0.8f, _size);

            var skin = bag.GetComponent<Renderer>();

            if (skin != null)
            {
                skin.material.color = new Color(0.93f, 0.78f, 0.22f);
            }

            // The bag is only what a thing looks like until the model its row names is up:
            // ContentModel finds the map preview itself, hides the renderer this object
            // already has and puts the real model under it, while the collider stays for the
            // right button to find. An item whose row names no model keeps its bag.
            if (!string.IsNullOrEmpty(model))
            {
                var picture = bag.AddComponent<ContentModel>();

                picture.Path = model;
                picture.Scale = _modelScale;
                picture.Rotation = _modelRotation;
            }

            _lying.Add(new Lying
            {
                Bag = bag,
                ItemId = itemId,
                From = from,
                To = spot,
                Left = _flight,
                Flight = _flight,
            });

            Said($"item {itemId} dropped, drawn from '{model}' at world " +
                 $"({spot.x:0.0}, {spot.y:0.0}, {spot.z:0.0}); {_lying.Count} on the ground");
        }

        private void Update()
        {
            Fly();
            Spin();

            var aimed = Aimed();

            UnderPointer = aimed != null;

            Clicked(aimed);
            Gathered();
        }

        /// <summary>The thing on the ground the pointer is over, or null when it is over none.</summary>
        private Lying Aimed()
        {
            var mouse = Mouse.current;
            var camera = Camera.main;

            if (mouse == null || camera == null)
            {
                return null;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return null;
            }

            var ray = camera.ScreenPointToRay(mouse.position.ReadValue());

            if (!Physics.Raycast(ray, out var hit, _click))
            {
                return null;
            }

            var lying = Find(hit.collider != null ? hit.collider.gameObject : null);

            // It is not enough for the pointer to be over a thing: the hero has to be almost
            // beside it. The client made the player walk to what he wanted to pick up, and a
            // click from across the map would otherwise reach it.
            return lying != null && Close(lying) ? lying : null;
        }

        /// <summary>Whether the hero is near enough to a thing on the ground to take it.</summary>
        private bool Close(Lying lying)
        {
            var hero = FindAnyObjectByType<HeroController>();
            var from = hero != null ? hero.transform.position : transform.position;

            var here = new Vector3(from.x, 0f, from.z);
            var there = new Vector3(lying.To.x, 0f, lying.To.z);

            return Vector3.Distance(here, there) <= _pick;
        }

        /// <summary>Walks every bag still in the air along its throw.</summary>
        private void Fly()
        {
            foreach (var lying in _lying)
            {
                if (lying.Bag == null || lying.Left <= 0f)
                {
                    continue;
                }

                lying.Left = Mathf.Max(0f, lying.Left - Time.deltaTime);

                var done = 1f - lying.Left / lying.Flight;
                var at = Vector3.Lerp(lying.From, lying.To, done);

                at.y += _arc * Mathf.Sin(Mathf.PI * done);

                lying.Bag.transform.position = lying.Left <= 0f ? lying.To : at;
            }
        }

        /// <summary>
        /// Turns and lifts what has landed, the way the mark a destination makes does:
        /// something lying perfectly still does not read as something to pick up.
        /// </summary>
        private void Spin()
        {
            foreach (var lying in _lying)
            {
                if (lying.Bag == null || lying.Left > 0f)
                {
                    continue;
                }

                lying.Bag.transform.Rotate(0f, _spin * Time.deltaTime, 0f, Space.World);

                var at = lying.To;

                at.y += _hover * (1f + Mathf.Sin(Time.time * 2f)) * 0.5f;

                lying.Bag.transform.position = at;
            }
        }

        /// <summary>
        /// The right button takes one thing back, whichever one it is pointing at. The left
        /// button belongs to the hero, who walks where it is pressed.
        /// </summary>
        private void Clicked(Lying aimed)
        {
            var mouse = Mouse.current;

            if (mouse == null || aimed == null)
            {
                return;
            }

            // The left button takes what it is aimed at, and the right button takes one back
            // whichever one it is pointing at: both are the client's own ways of gathering.
            if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)
            {
                Pick(aimed);
            }
        }

        /// <summary>Alt with A gathers everything lying within reach of the hero.</summary>
        private void Gathered()
        {
            var keyboard = Keyboard.current;

            if (keyboard == null || !keyboard.altKey.isPressed || !keyboard.aKey.wasPressedThisFrame)
            {
                return;
            }

            var hero = FindAnyObjectByType<HeroController>();
            var from = hero != null ? hero.transform.position : transform.position;
            var taken = 0;

            for (var i = _lying.Count - 1; i >= 0; i--)
            {
                var lying = _lying[i];

                // A thing still in the air has not landed yet, so it is not gathered.
                if (lying == null || lying.Bag == null || lying.Left > 0f)
                {
                    continue;
                }

                var here = new Vector3(from.x, 0f, from.z);
                var there = new Vector3(lying.To.x, 0f, lying.To.z);

                if (Vector3.Distance(here, there) > _gather)
                {
                    continue;
                }

                Pick(lying);
                taken++;
            }

            Said(taken == 0
                ? $"alt and A found nothing within {_gather:0} metres"
                : $"alt and A gathered {taken} thing(s) within {_gather:0} metres");
        }

        /// <summary>Which of the things on the ground this object is, if it is one of them.</summary>
        private Lying Find(GameObject bag)
        {
            if (bag == null)
            {
                return null;
            }

            foreach (var lying in _lying)
            {
                if (lying.Bag == bag)
                {
                    return lying;
                }
            }

            return null;
        }

        /// <summary>Hands one thing back to the bag it came out of and takes it off the ground.</summary>
        private void Pick(Lying lying)
        {
            var id = lying.ItemId;

            _lying.Remove(lying);

            if (lying.Bag != null)
            {
                Destroy(lying.Bag);
            }

            if (Picked != null)
            {
                Picked(id);
            }
            else
            {
                Said($"item {id} was dropped with nowhere to put it back");
            }
        }

        /// <summary>One line about what was dropped or taken, when it is being watched.</summary>
        private void Said(string what)
        {
            if (_debug)
            {
                Log.Info($"ground: {what}");
            }
        }
    }
}