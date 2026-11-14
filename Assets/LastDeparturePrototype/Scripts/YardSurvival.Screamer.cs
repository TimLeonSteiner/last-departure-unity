using System.Collections.Generic;
using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        const float ScreamRelease = .85f;
        const int ScreamSeconds = 3;
        const float ScreamEnd = ScreamRelease + ScreamSeconds;
        readonly Queue<Vector3> screamReinforcements = new Queue<Vector3>();

        static bool IsScreaming(Enemy enemy) =>
            enemy.hp > 0 && enemy.specialAttack == EnemySpecial.Scream &&
            enemy.attackTime >= ScreamRelease && enemy.attackTime < ScreamEnd;

        void AdvanceScream(Enemy screamer, float before)
        {
            if (!fighting || (dead && !PartnerCanContinue) || coreLost || IsCoopGuest || screamer.hp <= 0)
                return;

            if (before < ScreamRelease && screamer.attackTime >= ScreamRelease)
            {
                PlayScreamerCall(screamer);
                foreach (var nearby in enemies)
                {
                    if (nearby == screamer || nearby.hp <= 0 ||
                        (nearby.position - screamer.position).sqrMagnitude > 24 * 24)
                        continue;
                    nearby.rallyPoint = screamer.position;
                    nearby.rallyRemaining = 10;
                    nearby.approachTimer = 0;
                }
            }

            for (int second = 1; second <= ScreamSeconds; second++)
            {
                float threshold = ScreamRelease + second;
                if (before >= threshold || screamer.attackTime < threshold)
                    continue;

                screamReinforcements.Enqueue(screamer.position);
                screamReinforcements.Enqueue(screamer.position);
                remainingToSpawn += 2;
                spawnTimer = Mathf.Min(spawnTimer, .25f);
                if (second == 1)
                    ShowBanner("SCREAMER CALL — MORE INFECTED INCOMING");
            }
        }
    }
}
