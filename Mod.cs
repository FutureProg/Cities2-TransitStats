using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Game.Simulation;
using TransitStats.Systems;

namespace TransitStats
{
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger($"{nameof(TransitStats)}.{nameof(Mod)}").SetShowsErrorsInUI(false);

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info(nameof(OnLoad));
            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                log.Info($"Current mod asset at {asset.path}");

            updateSystem.UpdateAfter<CitizenBehaviorSystem, CitizenTransitTransferSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateBefore<CityStatisticsSystem, CitizenTransitTransferSystem>(SystemUpdatePhase.GameSimulation);
        }

        public void OnDispose()
        {
            log.Info(nameof(OnDispose));
        }
    }
}
