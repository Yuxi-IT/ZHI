using ZHI.Shared;

namespace ZHI.Watcher;

public partial class CosmosEngine : IDisposable
{
    private void ProcessActions(long[] actions, float[] chemicalValues, float[] rewards)
    {
        int n = _v.N;

        _depletedFoodSet.Clear();
        _depletedCorpsesSet.Clear();

        for (int i = 0; i < n; i++)
        {
            if (!_v.Alive[i]) continue;

            var action = (ZhiAction)actions[i];

            if (_v.IsEating[i])
            {
                bool stillEating = AutoExtractEating(i, rewards, _depletedFoodSet, _depletedCorpsesSet);
                if (!stillEating)
                    _v.IsEating[i] = false;
            }

            switch (action)
            {
                case ZhiAction.MoveUp:
                    ProcessMove(i, 0, -1, 0);
                    break;
                case ZhiAction.MoveDown:
                    ProcessMove(i, 0, 1, 1);
                    break;
                case ZhiAction.MoveLeft:
                    ProcessMove(i, -1, 0, 2);
                    break;
                case ZhiAction.MoveRight:
                    ProcessMove(i, 1, 0, 3);
                    break;

                case ZhiAction.Eat:
                    ProcessEat(i, rewards, _depletedFoodSet, _depletedCorpsesSet);
                    break;

                case ZhiAction.Attack:
                    _v.IsEating[i] = false;
                    ProcessAttack(i);
                    break;

                case ZhiAction.EmitChemical:
                    _v.IsEating[i] = false;
                    ProcessEmitChemical(i, chemicalValues[i]);
                    break;

                case ZhiAction.Drink:
                    _v.IsEating[i] = false;
                    if (_v.IsAdjacentToWater(_v.PosX[i], _v.PosY[i])
                        || _v.IsShallowWater(_v.PosX[i], _v.PosY[i]))
                    {
                        float thirstBefore = _v.Thirst[i];
                        _v.Thirst[i] = MathF.Min(100f, _v.Thirst[i] + _config.Thirst.DrinkRestore);
                        float thirstDelta = _v.Thirst[i] - thirstBefore;
                        if (thirstDelta > 0) rewards[i] += thirstDelta * 0.05f;
                    }
                    else
                    {
                        rewards[i] -= 0.1f;
                    }
                    break;
            }
        }

        // Cleanup depleted foods and corpses
        if (_depletedFoodSet.Count > 0 || _depletedCorpsesSet.Count > 0)
        {
            lock (_v.LockObj)
            {
                var sortedFood = _depletedFoodSet.OrderByDescending(x => x).ToList();
                foreach (int idx in sortedFood)
                    _v.FoodTiles.RemoveAt(idx);

                var sortedCorpses = _depletedCorpsesSet.OrderByDescending(x => x).ToList();
                foreach (int idx in sortedCorpses)
                    _v.CorpseTiles.RemoveAt(idx);
            }
        }
    }

    private void ProcessMove(int i, int dx, int dy, int facingDir)
    {
        int W = ToolDefinitions.GridWidth, H = ToolDefinitions.GridHeight;
        int fromX = _v.PosX[i], fromY = _v.PosY[i];
        int tx = fromX + dx, ty = fromY + dy;

        _v.IsEating[i] = false;
        if (tx >= 0 && tx < W && ty >= 0 && ty < H
            && !_v.IsMoundAt(tx, ty)
            && _v.GetCellOccupancy(tx, ty) < 2
            && (_v.Stamina[i] >= _config.Stamina.LowStaminaThreshold || _rng.NextDouble() > 0.5))
        {
            _v.MoveAgentCell(fromX, fromY, tx, ty);
            _v.PosX[i] = tx;
            _v.PosY[i] = ty;
            float moveCost = _config.Stamina.MoveCost * _v.BodySpeed[i];
            if (_v.GetTerrainAt(tx, ty) == ToolDefinitions.TerrainPit)
                moveCost += 2f;

            if (_v.IsShallowWater(tx, ty))
                moveCost += _config.Stamina.ShallowWaterMoveExtra;
            else if (_v.IsDeepWater(tx, ty))
                moveCost += _config.Stamina.DeepWaterMoveExtra;

            if (_v.IsDeepWater(fromX, fromY) && !_v.IsDeepWater(tx, ty) && !_v.IsShallowWater(tx, ty))
                moveCost += _config.Stamina.DeepWaterClimbExtra;

            _v.Stamina[i] = MathF.Max(0f, _v.Stamina[i] - moveCost);
        }
        else
        {
            _v.Stamina[i] = MathF.Max(0f, _v.Stamina[i] - _config.Stamina.MoveCost * 0.5f);
        }
        _v.ScentGrid[_v.PosX[i], _v.PosY[i]] += _config.Scent.DepositAmount;
        _v.FacingDirection[i] = facingDir;
    }

    private bool HasFoodOrCorpseAt(int px, int py)
    {
        lock (_v.LockObj)
        {
            foreach (var ft in _v.FoodTiles)
            {
                if (ft.X == px && ft.Y == py)
                    return true;
            }
            foreach (var ct in _v.CorpseTiles)
            {
                if (ct.X == px && ct.Y == py)
                    return true;
            }
        }
        return false;
    }
}
