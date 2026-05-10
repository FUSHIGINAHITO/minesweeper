using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Game))]
public class AutoSweeperBot : MonoBehaviour
{
    [Header("自动操作")]
    [SerializeField] private bool autoRun = true;
    [SerializeField] private float actionInterval = 0.1f;
    [SerializeField] private float autoRestartDelay = 0.15f;

    private float actionTimer;
    private float restartTimer;
    private bool waitingRestart;
    private bool botTimerRunning;

    private readonly Stack<Cell> revealStack = new();
    private readonly List<Cell> unknownNeighbors = new();
    private readonly List<Cell> flaggedNeighbors = new();
    private readonly List<Cell> diffBuffer = new();

    private void Update()
    {
        if (!autoRun || Game.instance == null || Game.instance.map == null)
        {
            return;
        }

        var game = Game.instance;
        var map = game.map;

        if (game.gameOver)
        {
            HandleAutoRestartOnGameOver(game, map);
            return;
        }

        if (botTimerRunning)
        {
            game.elapsedTime += Time.deltaTime;
        }

        actionTimer += Time.deltaTime;
        if (actionTimer < actionInterval)
        {
            return;
        }

        actionTimer -= actionInterval;
        StepOnce(game, map);
    }

    private void HandleAutoRestartOnGameOver(Game game, PeriodicMotifMap map)
    {
        if (!waitingRestart)
        {
            waitingRestart = true;
            restartTimer = 0f;
            return;
        }

        restartTimer += Time.deltaTime;
        if (restartTimer >= autoRestartDelay)
        {
            waitingRestart = false;
            restartTimer = 0f;
            actionTimer = 0f;
            botTimerRunning = false;
            game.Restart();
        }
    }

    private void StepOnce(Game game, PeriodicMotifMap map)
    {
        if (!map.minesPlaced)
        {
            // 首步随机点击，地图保证首击不踩雷
            var first = PickRandomUnknown(map);
            if (first != null)
            {
                LeftClickUnknown(game, map, first);
            }

            return;
        }

        // 规则 1：chord 平凡规则
        if (TryTrivialChordRule(game, map))
        {
            return;
        }

        // 规则 2：减法原理
        if (TrySubtractionRule(game, map))
        {
            return;
        }

        // 无确定步 -> 概率猜测
        var guess = PickByProbability(game, map);
        if (guess != null)
        {
            LeftClickUnknown(game, map, guess);
        }
    }

    private bool TryTrivialChordRule(Game game, PeriodicMotifMap map)
    {
        foreach (var c in map.cellList)
        {
            if (!c.isRevealed || c.value <= 0)
            {
                continue;
            }

            CollectNeighborState(c, unknownNeighbors, flaggedNeighbors);

            // 1) 未显示邻居数 == 数字 -> 这些未显示全是雷
            if (unknownNeighbors.Count > 0 && unknownNeighbors.Count == c.value)
            {
                bool didFlag = false;
                foreach (var u in unknownNeighbors)
                {
                    if (u.isFlagged)
                    {
                        continue;
                    }

                    RightClickFlag(game, u);
                    didFlag = true;
                }

                if (didFlag)
                {
                    CheckWin(game, map);
                    return true;
                }
            }

            // 2) 已标雷数 == 数字 -> 其余未知可扫开（仅在确实有动作时返回 true）
            if (unknownNeighbors.Count > 0 && flaggedNeighbors.Count == c.value)
            {
                if (ChordOpen(game, map, c))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool TrySubtractionRule(Game game, PeriodicMotifMap map)
    {
        foreach (var a in map.cellList)
        {
            if (!IsConstraintCell(a))
            {
                continue;
            }

            foreach (var b in a.neighbours)
            {
                if (!IsConstraintCell(b))
                {
                    continue;
                }

                // 防止重复 pair
                if (a.GetInstanceID() >= b.GetInstanceID())
                {
                    continue;
                }

                if (ApplySubtractionPair(game, map, a, b))
                {
                    return true;
                }

                if (ApplySubtractionPair(game, map, b, a))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // baseSet ⊆ superSet
    private bool ApplySubtractionPair(Game game, PeriodicMotifMap map, Cell baseCell, Cell superCell)
    {
        var baseUnknown = GetUnknownUnflaggedSet(baseCell);
        var superUnknown = GetUnknownUnflaggedSet(superCell);

        if (baseUnknown.Count == 0 || superUnknown.Count == 0)
        {
            return false;
        }

        if (!IsSubset(baseUnknown, superUnknown))
        {
            return false;
        }

        int baseRemain = RemainingMinesAround(baseCell);
        int superRemain = RemainingMinesAround(superCell);
        int diffMines = superRemain - baseRemain;

        diffBuffer.Clear();
        foreach (var c in superUnknown)
        {
            if (!baseUnknown.Contains(c))
            {
                diffBuffer.Add(c);
            }
        }

        if (diffBuffer.Count == 0)
        {
            return false;
        }

        // 差集全安全
        if (diffMines == 0)
        {
            LeftClickUnknown(game, map, diffBuffer[0]);
            return true;
        }

        // 差集全是雷
        if (diffMines == diffBuffer.Count)
        {
            RightClickFlag(game, diffBuffer[0]);
            CheckWin(game, map);
            return true;
        }

        return false;
    }

    private Cell PickByProbability(Game game, PeriodicMotifMap map)
    {
        var unknown = new List<Cell>(map.cellList.Count);
        foreach (var c in map.cellList)
        {
            if (!c.isRevealed && !c.isFlagged)
            {
                unknown.Add(c);
            }
        }

        if (unknown.Count == 0)
        {
            return null;
        }

        float globalP = Mathf.Clamp01(game.restMineCount / (float)unknown.Count);

        Cell best = null;
        float bestP = float.MaxValue;

        for (int i = 0; i < unknown.Count; i++)
        {
            var u = unknown[i];
            float p = EstimateMineProbability(u, globalP);

            if (p < bestP - 1e-6f)
            {
                bestP = p;
                best = u;
            }
            else if (Mathf.Abs(p - bestP) <= 1e-6f && Random.value < 0.5f)
            {
                best = u;
            }
        }

        return best;
    }

    private float EstimateMineProbability(Cell unknown, float fallbackP)
    {
        float sum = 0f;
        int count = 0;

        foreach (var n in unknown.neighbours)
        {
            // 仅使用已揭示数字格作为约束
            if (!n.isRevealed || n.value <= 0)
            {
                continue;
            }

            int u = 0;
            int f = 0;

            foreach (var nn in n.neighbours)
            {
                if (!nn.isRevealed)
                {
                    if (nn.isFlagged)
                    {
                        f++;
                    }
                    else
                    {
                        u++;
                    }
                }
            }

            if (u <= 0)
            {
                continue;
            }

            int remain = n.value - f;
            if (remain < 0)
            {
                continue;
            }

            sum += Mathf.Clamp01(remain / (float)u);
            count++;
        }

        return count > 0 ? sum / count : fallbackP;
    }

    private void LeftClickUnknown(Game game, PeriodicMotifMap map, Cell start)
    {
        if (start == null || start.isRevealed || start.isFlagged || game.gameOver)
        {
            return;
        }

        if (!map.minesPlaced)
        {
            map.PlaceMinesAvoiding(start);
            botTimerRunning = true;
        }

        RevealFrom(start, game, map);
    }

    // 返回值：本次 chord 是否真的产生了“扫开动作”
    private bool ChordOpen(Game game, PeriodicMotifMap map, Cell center)
    {
        if (center == null || !center.isRevealed || center.value <= 0 || game.gameOver)
        {
            return false;
        }

        int flagged = 0;
        int unknownUnflagged = 0;

        foreach (var n in center.neighbours)
        {
            if (n.isFlagged)
            {
                flagged++;
            }
            else if (!n.isRevealed)
            {
                unknownUnflagged++;
            }
        }

        // 没有可扫开的目标，直接判定“无效果”
        if (unknownUnflagged == 0)
        {
            return false;
        }

        if (flagged != center.value)
        {
            return false;
        }

        revealStack.Clear();
        foreach (var n in center.neighbours)
        {
            if (!n.isRevealed && !n.isFlagged)
            {
                revealStack.Push(n);
            }
        }

        RevealStack(game, map);
        return true;
    }

    private void RevealFrom(Cell start, Game game, PeriodicMotifMap map)
    {
        revealStack.Clear();
        revealStack.Push(start);
        RevealStack(game, map);
    }

    private void RevealStack(Game game, PeriodicMotifMap map)
    {
        while (revealStack.Count > 0)
        {
            var c = revealStack.Pop();
            if (c.isRevealed || c.isFlagged)
            {
                continue;
            }

            c.Reveal();

            // 已揭示后再判断地雷，不属于偷看
            if (c.isMine)
            {
                Defeat(game, map, c);
                return;
            }

            if (c.value == 0)
            {
                foreach (var n in c.neighbours)
                {
                    if (!n.isRevealed && !n.isFlagged)
                    {
                        revealStack.Push(n);
                    }
                }
            }
        }

        CheckWin(game, map);
    }

    private void RightClickFlag(Game game, Cell c)
    {
        if (c == null || c.isRevealed || c.isFlagged || game.gameOver)
        {
            return;
        }

        c.ToggleFlag();
        if (c.isFlagged)
        {
            game.restMineCount--;
        }
    }

    private void Defeat(Game game, PeriodicMotifMap map, Cell exploded)
    {
        if (game.gameOver)
        {
            return;
        }

        game.GameOver();
        map.ShowRestMines(exploded);
        UIManager.instance.Defeat();
        botTimerRunning = false;
    }

    private void CheckWin(Game game, PeriodicMotifMap map)
    {
        if (game.gameOver || !map.Win())
        {
            return;
        }

        game.GameOver();
        map.FlagRestMines();
        map.ShowVictoryAnim();
        game.restMineCount = 0;
        UIManager.instance.Victory();
        botTimerRunning = false;
    }

    private static void CollectNeighborState(Cell center, List<Cell> unknown, List<Cell> flagged)
    {
        unknown.Clear();
        flagged.Clear();

        foreach (var n in center.neighbours)
        {
            if (!n.isRevealed)
            {
                unknown.Add(n);
                if (n.isFlagged)
                {
                    flagged.Add(n);
                }
            }
        }
    }

    private static bool IsConstraintCell(Cell c)
    {
        return c != null && c.isRevealed && c.value > 0;
    }

    private static HashSet<Cell> GetUnknownUnflaggedSet(Cell c)
    {
        var set = new HashSet<Cell>();
        foreach (var n in c.neighbours)
        {
            if (!n.isRevealed && !n.isFlagged)
            {
                set.Add(n);
            }
        }

        return set;
    }

    private static bool IsSubset(HashSet<Cell> a, HashSet<Cell> b)
    {
        if (a.Count > b.Count)
        {
            return false;
        }

        foreach (var x in a)
        {
            if (!b.Contains(x))
            {
                return false;
            }
        }

        return true;
    }

    private static int RemainingMinesAround(Cell c)
    {
        int flagged = 0;
        foreach (var n in c.neighbours)
        {
            if (n.isFlagged)
            {
                flagged++;
            }
        }

        return c.value - flagged;
    }

    private static Cell PickRandomUnknown(PeriodicMotifMap map)
    {
        var candidates = new List<Cell>(map.cellList.Count);
        foreach (var c in map.cellList)
        {
            if (!c.isRevealed && !c.isFlagged)
            {
                candidates.Add(c);
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        int idx = Random.Range(0, candidates.Count);
        return candidates[idx];
    }
}