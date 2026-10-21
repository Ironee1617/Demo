using UnityEngine;

namespace PrototypeLab.Roguelike
{
    public sealed partial class TowerGame
    {
        private float oathEchoDelay;
        private Vector2 oathEchoOrigin, oathEchoTarget;
        private int oathEchoFacing;
        internal bool OathEchoPending { get { return oathEchoDelay > 0; } }

        internal void StartOathTrial(string companionId, int? fixedSeed = null)
        {
            NewRun(fixedSeed, companionId); oathLobby = false; intro = false;
            Notice(run.Oath.Name, 2.5f);
        }
        private void ClearOathEffects() { oathEchoDelay = 0; }
        private void OnOathSwordHit(Vector2 target)
        {
            if (run.Oath == null || hero.hp <= 0 || run.IsFloorCleared) return;
            bool wasReady = run.Oath.Ready;
            if (run.Oath.OnSwordHit())
            {
                Vector2 origin = hero.pos;
                ExecuteOath(origin, target, hero.facing, 1);
                if (run.Oath.Has(OathState.Echo))
                { oathEchoDelay = .3f; oathEchoOrigin = origin; oathEchoTarget = target; oathEchoFacing = hero.facing; }
            }
            else if (!wasReady && run.Oath.Ready)
                FloatText(hero.pos + Vector2.up * 2.7f, "서약 준비!", OathColor);
        }
        private Color OathColor { get { return run.Oath == null || run.Oath.CompanionId == "michael" ? TowerWorld.Gold :
            run.Oath.CompanionId == "gabriel" ? new Color(.55f,.8f,1) : TowerWorld.Teal; } }
        private void TickOath(float dt)
        {
            if (oathEchoDelay <= 0) return;
            if (hero.hp <= 0 || run.IsFloorCleared || run.Oath == null) { ClearOathEffects(); return; }
            oathEchoDelay -= dt;
            if (oathEchoDelay <= 0) ExecuteOath(oathEchoOrigin, oathEchoTarget, oathEchoFacing, .6f);
        }
        private void ExecuteOath(Vector2 origin, Vector2 target, int facing, float strength)
        {
            var oath = run.Oath; var owner = run.GetCompanion(oath.CompanionId);
            if (owner == null || hero.hp <= 0) return;
            float power = owner.Damage * owner.SkillPowerMultiplier * oath.Power * strength;
            Color color = OathColor;
            var actor = ActorFor(owner.Id);
            if (actor != null && vfx != null) vfx.Trail(actor.pos + Vector2.up, origin + Vector2.up, SkillEffect.ChainLightning, color);
            FloatText(origin + Vector2.up * 2.7f, strength < 1 ? "서약 메아리" : oath.Name, color);
            if (owner.Id == "michael")
            {
                bool circle = oath.Has(OathState.MichaelCircle);
                float radius = (circle ? 4.2f : 3.4f) * owner.AreaMultiplier;
                foreach (var enemy in enemies.ToArray())
                    if (CanTarget(enemy, owner) && (enemy.pos - origin).sqrMagnitude <= radius * radius &&
                        (circle || ((enemy.pos.x - origin.x) * facing >= -.4f && Mathf.Abs(enemy.pos.y - origin.y) < 2.4f)))
                        StrikeEnemy(enemy, power * 2.8f, owner, facing * .25f);
                if (vfx != null) vfx.Impact(SkillEffect.BladeStorm, origin + Vector2.up, radius, color);
                SlashArc(origin + new Vector2(facing, 1), facing, color);
                if (oath.Has(OathState.MichaelWave))
                    FireOathRay(origin + Vector2.up, Vector2.right * facing, power * 2.8f * .55f, owner, 7, false, SkillEffect.Shockwave);
            }
            else if (owner.Id == "gabriel")
            {
                Vector2 from = origin + Vector2.up * .7f;
                Vector2 direction = target + Vector2.up * .7f - from;
                if (direction.sqrMagnitude < .01f) direction = Vector2.right * facing;
                direction.Normalize();
                bool fan = oath.Has(OathState.GabrielFan);
                for (int i = fan ? -1 : 0; i <= (fan ? 1 : 0); i++)
                    FireOathRay(from, Quaternion.Euler(0, 0, i * 12) * direction, power * 4 * (fan ? .65f : 1),
                        owner, 12, oath.Has(OathState.GabrielChain), SkillEffect.PierceShot);
            }
            else
            {
                float heal = power * .8f * owner.HealingMultiplier;
                float overflow = Mathf.Max(0, heal * run.Player.HealingMultiplier - (PlayerMaxHealth - hero.hp));
                HealPlayer(heal, true);
                float shield = power * 1.5f * owner.HealingMultiplier * run.Player.HealingMultiplier;
                if (oath.Has(OathState.RaphaelOverflow)) shield += overflow * 2;
                playerShield = Mathf.Min(PlayerMaxHealth * .65f, playerShield + shield);
                shieldTime = Mathf.Max(shieldTime, 3);
                if (vfx != null) vfx.Cast(SkillEffect.ShieldPulse, hero.pos + Vector2.up, 1.5f, color);
                if (oath.Has(OathState.RaphaelGarden))
                    AddArea(owner, hero, SkillEffect.HealPulse, origin, 2.2f, 3, .5f, power / 6 * owner.HealingMultiplier, color, 1);
            }
            Sound(chimeSound, .45f);
        }
        private void FireOathRay(Vector2 from, Vector2 direction, float damage, HeroState owner, float range, bool chain, SkillEffect style)
        {
            Fire(from, from + direction, 18, damage, false, OathColor, 0, owner, 99);
            StyleLastShot(style); LimitLastShotRange(range, 18);
            shots[shots.Count - 1].oathChain = chain;
        }
        private void ChainOathShot(Shot shot, Fighter first)
        {
            if (!shot.oathChain) return;
            shot.oathChain = false;
            // Trigger only on the ray's first actual contact, not when the sword launches it.
            var nearby = enemies.FindAll(e => e != first && CanTarget(e, shot.owner) && Vector2.Distance(e.pos, first.pos) <= 3.5f);
            nearby.Sort((a,b) => (a.pos-first.pos).sqrMagnitude.CompareTo((b.pos-first.pos).sqrMagnitude));
            for (int i = 0; i < Mathf.Min(2, nearby.Count); i++)
            {
                if (vfx != null) vfx.Trail(first.pos + Vector2.up, nearby[i].pos + Vector2.up, SkillEffect.ChainLightning, shot.color);
                StrikeEnemy(nearby[i], shot.damage * .5f, shot.owner, 0);
            }
        }
    }
}
