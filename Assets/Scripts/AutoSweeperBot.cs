using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Game))]
public class AutoSweeperBot : MonoBehaviour
{
    [Header("自动运行")]
    [SerializeField] private bool autoRun = true;
    [SerializeField] private bool autoRestart = true;
    [SerializeField] private float actionInterval = 0.1f;
    [SerializeField] private float autoRestartDelay = 0.15f;

    [Header("策略开关（按顺序尝试）")]
    [SerializeField] private bool useTrivialRule = true;
    [SerializeField] private bool useSubtractionRule = true;
    [SerializeField] private bool useConstraintBlockEnumeration = true;
    [SerializeField] private bool useRemainingMineConstraint = true;
    [SerializeField] private bool useBestProbabilityGuess = true;

    [Header("枚举限制")]
    [SerializeField] private int maxEnumerationVarsPerBlock = 18;
    [SerializeField] private int maxEnumerationSolutionsPerBlock = 200000;

    private float actionTimer;
    private float restartTimer;
    private bool waitingRestart;
    private bool roundLogged;
    private int roundIndex = 1;

    private int trivialUsed;
    private int subtractionUsed;
    private int enumerationUsed;
    private int remainingConstraintUsed;
    private int probabilityGuessUsed;
    private int randomGuessUsed;

    private readonly List<Cell> unknownNeighbors = new();
    private readonly List<Cell> flaggedNeighbors = new();
    private readonly List<Cell> diffBuffer = new();
    private readonly List<Cell> unknownCache = new();

    private sealed class Constraint
    {
        public int[] vars;
        public int required;
    }

    private enum EnumeratedAction
    {
        None,
        OpenSafe,
        FlagMine
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.A))
        {
            autoRun = !autoRun;
        }

        if (!autoRun || Game.instance == null || Game.instance.map == null)
        {
            return;
        }

        var game = Game.instance;
        var map = game.map;

        if (!game.gameOver && roundLogged)
        {
            BeginNewRound();
        }

        if (game.gameOver)
        {
            if (!roundLogged)
            {
                LogRoundResult(game, map);
                roundLogged = true;
            }

            HandleAutoRestartOnGameOver(game);
            return;
        }

        actionTimer += Time.deltaTime;
        if (actionTimer < actionInterval)
        {
            return;
        }

        actionTimer -= actionInterval;
        StepOnce(game, map);
    }

    private void BeginNewRound()
    {
        waitingRestart = false;
        restartTimer = 0f;
        actionTimer = 0f;

        trivialUsed = 0;
        subtractionUsed = 0;
        enumerationUsed = 0;
        remainingConstraintUsed = 0;
        probabilityGuessUsed = 0;
        randomGuessUsed = 0;

        roundLogged = false;
        roundIndex++;
    }

    private void LogRoundResult(Game game, PeriodicMotifMap map)
    {
        bool win = map.Win();

        Debug.Log(
            "[AutoSweeperBot] Round " + roundIndex
            + " | Result=" + (win ? "WIN" : "LOSE")
            + " | Trivial=" + trivialUsed
            + " | Subtraction=" + subtractionUsed
            + " | Enumeration=" + enumerationUsed
            + " | RemainingConstraint=" + remainingConstraintUsed
            + " | ProbabilityGuess=" + probabilityGuessUsed
            + " | RandomGuess=" + randomGuessUsed
            + " | Enabled=["
            + "Trivial:" + useTrivialRule + ", "
            + "Subtraction:" + useSubtractionRule + ", "
            + "Enumeration:" + useConstraintBlockEnumeration + ", "
            + "Remaining:" + useRemainingMineConstraint + ", "
            + "Probability:" + useBestProbabilityGuess + "]");
    }

    private void HandleAutoRestartOnGameOver(Game game)
    {
        if (!waitingRestart)
        {
            waitingRestart = true;
            restartTimer = 0f;
            return;
        }

        restartTimer += Time.deltaTime;
        if (autoRestart && restartTimer >= autoRestartDelay)
        {
            waitingRestart = false;
            restartTimer = 0f;
            actionTimer = 0f;
            game.Restart();
        }
    }

    private void StepOnce(Game game, PeriodicMotifMap map)
    {
        if (!map.minesPlaced)
        {
            var first = PickRandomUnknown(map);
            if (first != null && game.TryOpenCell(first))
            {
                randomGuessUsed++;
            }

            return;
        }

        if (useTrivialRule && TryTrivialChordRule(game, map))
        {
            trivialUsed++;
            return;
        }

        if (useSubtractionRule && TrySubtractionRule(game, map))
        {
            subtractionUsed++;
            return;
        }

        if (useConstraintBlockEnumeration && TryConstraintBlockEnumeration(game, map))
        {
            enumerationUsed++;
            return;
        }

        if (useRemainingMineConstraint && TryRemainingMineConstraint(game, map))
        {
            remainingConstraintUsed++;
            return;
        }

        if (useBestProbabilityGuess)
        {
            var guess = PickByProbability(game, map);
            if (guess != null && game.TryOpenCell(guess))
            {
                probabilityGuessUsed++;
                return;
            }
        }

        var fallback = PickRandomUnknown(map);
        if (fallback != null && game.TryOpenCell(fallback))
        {
            randomGuessUsed++;
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

            if (unknownNeighbors.Count <= 0)
            {
                continue;
            }

            // 规则A：未显示邻居总数 == 数字 -> 未显示邻居全是雷（逐个右键）
            if (unknownNeighbors.Count == c.value)
            {
                foreach (var u in unknownNeighbors)
                {
                    if (!u.isFlagged && game.TryFlagCell(u))
                    {
                        return true; // 每 0.1s 只做一次“点击”
                    }
                }
            }

            // 规则B：已标雷数 == 数字 -> 可 chord
            if (flaggedNeighbors.Count == c.value && game.TryChord(c))
            {
                return true; // 这是一次 chord 点击
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

                if (a.GetInstanceID() >= b.GetInstanceID())
                {
                    continue;
                }

                if (ApplySubtractionPair(game, a, b))
                {
                    return true;
                }

                if (ApplySubtractionPair(game, b, a))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool ApplySubtractionPair(Game game, Cell baseCell, Cell superCell)
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

        if (diffMines == 0)
        {
            return game.TryOpenCell(diffBuffer[0]);
        }

        if (diffMines == diffBuffer.Count)
        {
            return game.TryFlagCell(diffBuffer[0]);
        }

        return false;
    }

    private bool TryConstraintBlockEnumeration(Game game, PeriodicMotifMap map)
    {
        if (!BuildConstraints(map, out var vars, out var constraints))
        {
            return false;
        }

        int varCount = vars.Count;
        int conCount = constraints.Count;
        if (varCount == 0 || conCount == 0)
        {
            return false;
        }

        var varToCons = new List<int>[varCount];
        for (int i = 0; i < varCount; i++)
        {
            varToCons[i] = new List<int>(4);
        }

        for (int ci = 0; ci < conCount; ci++)
        {
            var c = constraints[ci];
            for (int k = 0; k < c.vars.Length; k++)
            {
                varToCons[c.vars[k]].Add(ci);
            }
        }

        var varVisited = new bool[varCount];
        var conVisited = new bool[conCount];
        var qVar = new Queue<int>();
        var qCon = new Queue<int>();

        var compVars = new List<int>(32);
        var compCons = new List<int>(32);

        for (int sv = 0; sv < varCount; sv++)
        {
            if (varVisited[sv])
            {
                continue;
            }

            compVars.Clear();
            compCons.Clear();

            varVisited[sv] = true;
            qVar.Enqueue(sv);

            while (qVar.Count > 0 || qCon.Count > 0)
            {
                while (qVar.Count > 0)
                {
                    int v = qVar.Dequeue();
                    compVars.Add(v);

                    var linkedCons = varToCons[v];
                    for (int i = 0; i < linkedCons.Count; i++)
                    {
                        int ci = linkedCons[i];
                        if (conVisited[ci])
                        {
                            continue;
                        }

                        conVisited[ci] = true;
                        qCon.Enqueue(ci);
                    }
                }

                while (qCon.Count > 0)
                {
                    int ci = qCon.Dequeue();
                    compCons.Add(ci);

                    var c = constraints[ci];
                    for (int i = 0; i < c.vars.Length; i++)
                    {
                        int v = c.vars[i];
                        if (varVisited[v])
                        {
                            continue;
                        }

                        varVisited[v] = true;
                        qVar.Enqueue(v);
                    }
                }
            }

            if (compVars.Count == 0 || compCons.Count == 0)
            {
                continue;
            }

            if (TryEnumerateComponent(
                vars,
                constraints,
                compVars,
                compCons,
                out var action,
                out var actionCell))
            {
                if (action == EnumeratedAction.OpenSafe)
                {
                    return game.TryOpenCell(actionCell);
                }

                if (action == EnumeratedAction.FlagMine)
                {
                    return game.TryFlagCell(actionCell);
                }
            }
        }

        return false;
    }

    private bool BuildConstraints(PeriodicMotifMap map, out List<Cell> vars, out List<Constraint> constraints)
    {
        vars = new List<Cell>(128);
        constraints = new List<Constraint>(128);

        var varIndex = new Dictionary<Cell, int>(256);

        foreach (var r in map.cellList)
        {
            if (!r.isRevealed || r.value <= 0)
            {
                continue;
            }

            int flagged = 0;
            unknownCache.Clear();

            foreach (var n in r.neighbours)
            {
                if (!n.isRevealed)
                {
                    if (n.isFlagged)
                    {
                        flagged++;
                    }
                    else
                    {
                        unknownCache.Add(n);
                    }
                }
            }

            if (unknownCache.Count == 0)
            {
                continue;
            }

            int remain = r.value - flagged;
            if (remain < 0 || remain > unknownCache.Count)
            {
                continue;
            }

            var idx = new int[unknownCache.Count];
            for (int i = 0; i < unknownCache.Count; i++)
            {
                var cell = unknownCache[i];
                if (!varIndex.TryGetValue(cell, out int vi))
                {
                    vi = vars.Count;
                    vars.Add(cell);
                    varIndex.Add(cell, vi);
                }

                idx[i] = vi;
            }

            constraints.Add(new Constraint
            {
                vars = idx,
                required = remain
            });
        }

        return vars.Count > 0 && constraints.Count > 0;
    }

    private bool TryEnumerateComponent(
        List<Cell> globalVars,
        List<Constraint> globalConstraints,
        List<int> compVars,
        List<int> compCons,
        out EnumeratedAction action,
        out Cell actionCell)
    {
        action = EnumeratedAction.None;
        actionCell = null;

        if (compVars.Count > maxEnumerationVarsPerBlock)
        {
            return false;
        }

        int localVarCount = compVars.Count;
        int localConCount = compCons.Count;

        var globalToLocal = new Dictionary<int, int>(localVarCount);
        for (int i = 0; i < localVarCount; i++)
        {
            globalToLocal[compVars[i]] = i;
        }

        var localConstraints = new (int[] vars, int required)[localConCount];
        for (int i = 0; i < localConCount; i++)
        {
            var gc = globalConstraints[compCons[i]];
            var lvars = new int[gc.vars.Length];

            for (int k = 0; k < gc.vars.Length; k++)
            {
                lvars[k] = globalToLocal[gc.vars[k]];
            }

            localConstraints[i] = (lvars, gc.required);
        }

        var varToLocalCons = new List<int>[localVarCount];
        for (int i = 0; i < localVarCount; i++)
        {
            varToLocalCons[i] = new List<int>(4);
        }

        for (int ci = 0; ci < localConCount; ci++)
        {
            var lc = localConstraints[ci];
            for (int k = 0; k < lc.vars.Length; k++)
            {
                varToLocalCons[lc.vars[k]].Add(ci);
            }
        }

        var order = new int[localVarCount];
        for (int i = 0; i < localVarCount; i++)
        {
            order[i] = i;
        }

        Array.Sort(order, (a, b) => varToLocalCons[b].Count.CompareTo(varToLocalCons[a].Count));

        var assignment = new int[localVarCount];
        for (int i = 0; i < localVarCount; i++)
        {
            assignment[i] = -1;
        }

        var assignedCount = new int[localConCount];
        var mineCount = new int[localConCount];
        var mineAppear = new int[localVarCount];

        int totalSolutions = 0;
        bool aborted = false;

        void Dfs(int depth)
        {
            if (aborted)
            {
                return;
            }

            if (totalSolutions > maxEnumerationSolutionsPerBlock)
            {
                aborted = true;
                return;
            }

            if (depth == localVarCount)
            {
                totalSolutions++;
                for (int i = 0; i < localVarCount; i++)
                {
                    if (assignment[i] == 1)
                    {
                        mineAppear[i]++;
                    }
                }

                return;
            }

            int v = order[depth];
            var linkedCons = varToLocalCons[v];

            for (int val = 0; val <= 1; val++)
            {
                assignment[v] = val;
                bool valid = true;

                for (int i = 0; i < linkedCons.Count; i++)
                {
                    int ci = linkedCons[i];
                    assignedCount[ci]++;
                    mineCount[ci] += val;

                    var lc = localConstraints[ci];
                    int minPossible = mineCount[ci];
                    int maxPossible = mineCount[ci] + (lc.vars.Length - assignedCount[ci]);

                    if (lc.required < minPossible || lc.required > maxPossible)
                    {
                        valid = false;
                    }
                }

                if (valid)
                {
                    Dfs(depth + 1);
                }

                for (int i = 0; i < linkedCons.Count; i++)
                {
                    int ci = linkedCons[i];
                    assignedCount[ci]--;
                    mineCount[ci] -= val;
                }

                assignment[v] = -1;
            }
        }

        Dfs(0);

        if (aborted || totalSolutions <= 0)
        {
            return false;
        }

        // 优先找“必安全”，其次“必为雷”
        for (int lv = 0; lv < localVarCount; lv++)
        {
            if (mineAppear[lv] == 0)
            {
                action = EnumeratedAction.OpenSafe;
                actionCell = globalVars[compVars[lv]];
                return true;
            }
        }

        for (int lv = 0; lv < localVarCount; lv++)
        {
            if (mineAppear[lv] == totalSolutions)
            {
                action = EnumeratedAction.FlagMine;
                actionCell = globalVars[compVars[lv]];
                return true;
            }
        }

        return false;
    }

    private bool TryRemainingMineConstraint(Game game, PeriodicMotifMap map)
    {
        unknownCache.Clear();
        foreach (var c in map.cellList)
        {
            if (!c.isRevealed && !c.isFlagged)
            {
                unknownCache.Add(c);
            }
        }

        int unknownCount = unknownCache.Count;
        if (unknownCount == 0)
        {
            return false;
        }

        int remainMines = game.restMineCount;

        // 剩余雷为 0：全安全
        if (remainMines == 0)
        {
            return game.TryOpenCell(unknownCache[0]);
        }

        // 未知格数量 == 剩余雷数：全是雷
        if (remainMines == unknownCount)
        {
            return game.TryFlagCell(unknownCache[0]);
        }

        return false;
    }

    private Cell PickByProbability(Game game, PeriodicMotifMap map)
    {
        unknownCache.Clear();
        foreach (var c in map.cellList)
        {
            if (!c.isRevealed && !c.isFlagged)
            {
                unknownCache.Add(c);
            }
        }

        if (unknownCache.Count == 0)
        {
            return null;
        }

        float globalMineP = Mathf.Clamp01(game.restMineCount / (float)unknownCache.Count);

        Cell best = null;
        float bestSafeP = float.NegativeInfinity;

        for (int i = 0; i < unknownCache.Count; i++)
        {
            var u = unknownCache[i];
            float mineP = EstimateMineProbability(u, globalMineP);
            float safeP = 1f - mineP; // “概率最高”按“安全概率最高”解释

            if (safeP > bestSafeP + 1e-6f)
            {
                bestSafeP = safeP;
                best = u;
            }
            else if (Mathf.Abs(safeP - bestSafeP) <= 1e-6f && UnityEngine.Random.value < 0.5f)
            {
                best = u;
            }
        }

        return best;
    }

    private static float EstimateMineProbability(Cell unknown, float fallbackMineP)
    {
        float sum = 0f;
        int count = 0;

        foreach (var n in unknown.neighbours)
        {
            // 仅使用“已揭示数字格”约束
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

        return count > 0 ? (sum / count) : fallbackMineP;
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

        int idx = UnityEngine.Random.Range(0, candidates.Count);
        return candidates[idx];
    }
}