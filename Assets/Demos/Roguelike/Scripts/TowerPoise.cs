using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Poise and the player's sliding knockback. Every monster but a boss carries poise: LightPoise for the light kinds (0,
    // 1 and the flyer 3), HeavyPoise for the heavy kind 2, ElitePoiseScale times that for an elite. Only the player's own
    // blows take it: a sword cut SwordPoise, a heavy blow (dash slash, finisher, counter) HeavyBlowPoise, a gun bullet, an
    // orb bolt or a reflected bolt ShotPoise each, a fast fall's landing SlamPoise. Companions, skills and damage over time
    // take none. PoiseRefill seconds without such a blow fill it again.
    // Emptied, it breaks: the windup (with a lunge, a second blow or a leap's crouch) is cancelled, which hands its attack
    // token back, the monster reels StaggerTime in its hurt pose, attacks again no sooner than StaggerCooldown and is
    // knocked BreakPush back. For BreakGuard after a break other blows, a dash slash included, take no poise and only a
    // counter breaks it again, so neither holding the attack nor dashing in and out against a crowd keeps it from striking.
    // The same blows knock back over time rather than at once: KnockbackRate times the push at first, easing off at that
    // rate, so the same distance plays out over about .15 s inside the chamber's walls. Skills and companions still push at
    // once, and bosses keep BossPushScale of an instant push.
    public sealed partial class TowerGame
    {
        internal const float LightPoise = 3, HeavyPoise = 5, ElitePoiseScale = 1.5f, PoiseRefill = 1;
        internal const float SwordPoise = 1, HeavyBlowPoise = 2, ShotPoise = .5f, SlamPoise = 1;
        internal const float StaggerTime = .35f, StaggerCooldown = .6f, BreakPush = 1.2f, BreakGuard = 1.5f;
        // A slide stops once less than a hundredth of a unit is left of it. While it still runs faster than KnockbackHold the
        // monster neither walks nor begins a lunging blow or a leap: about .04 s after a plain cut, .12 s after a heavy blow,
        // never after a lone bullet (1.2 u/s), so even a fast gun cannot hold a monster in place.
        internal const float KnockbackRate = 12, KnockbackRest = .12f, KnockbackHold = 2.5f;
        internal int PoiseBreaks { get; private set; }

        /// <summary>The poise <paramref name="actor"/> holds when full; none for a boss.</summary>
        internal static float MaxPoise(Fighter actor)
        { return actor == null || actor.isBoss ? 0 : (actor.kind == 2 ? HeavyPoise : LightPoise) * (actor.isElite ? ElitePoiseScale : 1); }

        /// <summary>Whether the player's own blows take <paramref name="actor"/>'s poise and slide it back: a monster, not a boss, off the practice range.</summary>
        private bool Staggerable(Fighter actor) { return actor != null && actor.isEnemy && !actor.isBoss && !practice; }

        /// <summary>Whether the player's knockback still carries <paramref name="actor"/> too fast to walk, begin a lunging blow or leap.</summary>
        private static bool Knocked(Fighter actor) { return Mathf.Abs(actor.moveVelocity) > KnockbackHold; }

        /// <summary>
        /// Follows a blow of the player's own that struck <paramref name="actor"/> without a push of its own: takes
        /// <paramref name="poise"/> and breaks it once emptied (a <paramref name="breaker"/>, only the counter, breaks it
        /// within a break's guard too), then knocks it back over time by <paramref name="push"/>, or by BreakPush
        /// the same way when it broke. A boss, a practice target or a fallen monster is left alone.
        /// </summary>
        private void PlayerBlow(Fighter actor, float push, float poise, bool breaker)
        {
            if (!Staggerable(actor) || actor.hp <= 0) return;
            bool broke;
            if (actor.poiseGuard > 0) broke = breaker;
            else { actor.poiseDamage += poise; actor.poiseRest = PoiseRefill; broke = actor.poiseDamage >= MaxPoise(actor) - .001f; }
            if (broke) { BreakPoise(actor); push = (push < 0 ? -1 : 1) * BreakPush; }
            // Added to a slide still under way, as instant pushes add up.
            actor.moveVelocity += push * KnockbackRate;
        }

        private void BreakPoise(Fighter actor)
        {
            EnemyState state;
            if (enemyCombat.TryGetValue(actor, out state)) CancelEnemyWindup(actor, state); else actor.windup = 0;
            actor.cooldown = Mathf.Max(actor.cooldown, StaggerCooldown);
            actor.staggerTime = StaggerTime; actor.poiseGuard = BreakGuard; actor.poiseDamage = actor.poiseRest = 0;
            // The windup's motion gives way to the hurt pose; the telegraph flash goes out on the next tick with the windup.
            actor.attackPose = 0; if (actor.art != null) actor.art.Stagger(StaggerTime);
            PoiseBreaks++;
            AddNumber(actor.pos + Vector2.up * 2.4f, "경직", TowerWorld.Teal, .75f, .6f);
            if (vfx != null) vfx.ImpactRing(actor.pos + Vector2.up * .9f, .5f, TowerWorld.Gold);
            Sound(heavyCue, .35f);
        }

        /// <summary>Runs a monster's stagger, break guard and poise refill down, and slides its knockback out inside the walls.</summary>
        private void TickPoise(Fighter actor, float dt)
        {
            actor.staggerTime = Mathf.Max(0, actor.staggerTime - dt);
            actor.poiseGuard = Mathf.Max(0, actor.poiseGuard - dt);
            if (actor.poiseRest > 0 && (actor.poiseRest -= dt) <= 0) actor.poiseRest = actor.poiseDamage = 0;
            if (actor.moveVelocity == 0 || dt <= 0) return;
            float eased = 1 - Mathf.Exp(-KnockbackRate * dt), x = actor.pos.x + actor.moveVelocity * eased / KnockbackRate;
            actor.pos.x = Mathf.Clamp(x, TowerWorld.Left + .8f, TowerWorld.Right - .8f);
            // A wall stops the slide; otherwise it eases off until next to nothing is left. The ground under the new spot is
            // followed (or a ledge left) by the gravity step after.
            float left = actor.moveVelocity * (1 - eased);
            actor.moveVelocity = actor.pos.x != x || Mathf.Abs(left) < KnockbackRest ? 0 : left;
        }
    }
}
