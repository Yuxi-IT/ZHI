using ZHI.Shared;

namespace ZHI.Watcher;

public partial class CosmosEngine
{
    /// <summary>
    /// Auto-extract energy for agent in eating toggle state. Returns true if still eating.
    /// </summary>
    private bool AutoExtractEating(int i, float[] rewards, HashSet<int> depletedFood, HashSet<int> depletedCorpses)
    {
        int px = _v.PosX[i];
        int py = _v.PosY[i];
        float extracted = 0f;
        string foodType = "";

        lock (_v.LockObj)
        {
            for (int f = _v.FoodTiles.Count - 1; f >= 0; f--)
            {
                var ft = _v.FoodTiles[f];
                if (ft.X != px || ft.Y != py) continue;

                int eaters = CountEatersOnFood(ft, i);
                float efficiency = 1f / MathF.Sqrt(eaters);
                float perTick = _config.Grid.FoodPerTickEnergy * efficiency;
                extracted = MathF.Min(perTick, ft.Energy);
                ft.Energy -= extracted;
                foodType = "Food";

                if (ft.Energy <= 0.001f)
                {
                    depletedFood.Add(f);
                    _genFoodEaten++;
                }
                else
                    _v.FoodTiles[f] = ft;

                break;
            }

            if (extracted <= 0f)
            {
                for (int c = _v.CorpseTiles.Count - 1; c >= 0; c--)
                {
                    var ct = _v.CorpseTiles[c];
                    if (ct.X != px || ct.Y != py) continue;

                    float perTick = _config.Grid.CorpsePerTickEnergy;
                    extracted = MathF.Min(perTick, ct.Energy);
                    ct.Energy -= extracted;
                    foodType = "Corpse";

                    if (ct.Energy <= 0.001f)
                    {
                        depletedCorpses.Add(c);
                        _genCorpsesEaten++;
                    }
                    else
                        _v.CorpseTiles[c] = ct;

                    break;
                }
            }
        }

        if (extracted <= 0f)
            return false;

        float hungerBefore = _v.Hunger[i];
        _v.Hunger[i] = MathF.Min(100f, _v.Hunger[i] + extracted);
        float hungerDelta = _v.Hunger[i] - hungerBefore;
        if (hungerDelta > 0) rewards[i] += hungerDelta * 0.05f;

        _v.EatCount[i]++;
        if (foodType == "Corpse")
        {
            _v.CorpseEatCount[i]++;
            _genCorpseEnergy += extracted;
        }
        else
        {
            _v.FoodEatCount[i]++;
            _genFoodEnergy += extracted;
        }

        _tickEvents.Add(new WorldEvent { Type = "eat", AgentId = i, FoodType = foodType, Value = extracted, Tick = _globalTick });
        return true;
    }

    private int CountEatersOnFood(FoodTile ft, int excludeIdx)
    {
        int count = 1;
        for (int j = 0; j < _v.N; j++)
        {
            if (j == excludeIdx || !_v.Alive[j] || !_v.IsEating[j]) continue;
            if (_v.PosX[j] == ft.X && _v.PosY[j] == ft.Y)
                count++;
        }
        return count;
    }

    private void ProcessEat(int i, float[] rewards, HashSet<int> depletedFood, HashSet<int> depletedCorpses)
    {
        if (_v.IsEating[i])
        {
            _v.IsEating[i] = false;
            return;
        }

        bool hasFood = HasFoodOrCorpseAt(_v.PosX[i], _v.PosY[i]);
        if (!hasFood)
        {
            rewards[i] -= 0.25f;
            return;
        }

        _v.IsEating[i] = true;
    }
}
