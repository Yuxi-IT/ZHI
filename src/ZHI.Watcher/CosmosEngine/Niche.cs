namespace ZHI.Watcher;

public partial class CosmosEngine
{
    private int _nextNicheTick;

    /// <summary>
    /// Classify agents into ecological niches based on dietary history.
    /// 0=Omnivore (balanced), 1=Herbivore (mostly plants), 2=Carnivore (mostly attacks), 3=Scavenger (mostly corpses).
    /// </summary>
    private void ComputeNiches()
    {
        if (_globalTick < _nextNicheTick) return;
        _nextNicheTick = _globalTick + 200;

        int n = _v.N;
        for (int i = 0; i < n; i++)
        {
            if (!_v.Alive[i]) continue;

            int food = _v.FoodEatCount[i];
            int corpse = _v.CorpseEatCount[i];
            int attack = _v.AttackCount[i];
            int total = food + corpse + attack;

            if (total < 10)
            {
                _v.Niche[i] = 0; // insufficient data
                continue;
            }

            float foodRatio = (float)food / total;
            float corpseRatio = (float)corpse / total;
            float attackRatio = (float)attack / total;

            if (foodRatio > 0.7f)
                _v.Niche[i] = 1; // Herbivore
            else if (attackRatio > 0.5f)
                _v.Niche[i] = 2; // Carnivore
            else if (corpseRatio > 0.5f)
                _v.Niche[i] = 3; // Scavenger
            else
                _v.Niche[i] = 0; // Omnivore
        }
    }
}
