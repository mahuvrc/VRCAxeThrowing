using System;
using UdonSharp;
using UnityEngine;

namespace mahu.AxeThrowing
{
    public abstract class AxeThrowingGameMode : UdonSharpBehaviour
    {
        public AxeThrowingGame game;

        public GameObject[] ChildObjects;

        public abstract string DisplayName { get; }

        // TODO: Refactor player change code
        [UdonSynced, NonSerialized]
        public bool PlayerOpening;

        public abstract void _ScoreAxe();

        public abstract void _AxeTaken();

        public abstract void _ConsumeAxe();

        public abstract void _NextRound();

        public abstract void _Reset();

        public virtual void _Show()
        {
            foreach (var item in ChildObjects)
            {
                item.SetActive(true);
            }
        }

        public virtual void _Hide()
        {
            foreach (var item in ChildObjects)
            {
                item.SetActive(false);
            }
        }

        public bool IsActiveGamemode()
        {
            return game != null && game.ActiveGameMode == this;
        }

        public bool IsAxeInSphereScoreZone(SphereCollider sphereCollider)
        {
            var collisions = Physics.OverlapSphere(sphereCollider.transform.position + sphereCollider.center, sphereCollider.radius * sphereCollider.transform.lossyScale.x, ~0, QueryTriggerInteraction.Collide);
            foreach (var collider in collisions)
            {
                if (game.Axe.scoreCollider == collider)
                {
                    return true;
                }
            }

            return false;
        }
    }
}