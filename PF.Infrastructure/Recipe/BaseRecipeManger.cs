using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Recipe;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Infrastructure.Recipe
{
    public class BaseRecipeManger<T> : IRecipeManger<T> where T : ReceipeParamBase
    {

        public readonly Logging.CategoryLogger RecipeLogger;
        public BaseRecipeManger(ILogService logger)
        {
            if (File.Exists(filepath))
            {
                string str = File.ReadAllText(filepath);
                _stationRecipeDic = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, T>>(str);
            }
            else
            {
                _stationRecipeDic = new Dictionary<string, T>();
            }
            RecipeLogger = Logging.CategoryLoggerFactory.Recipe(logger);
        }


        private readonly Dictionary<string, T> _stationRecipeDic = new Dictionary<string, T>();

        public Dictionary<string, T> StationRecipeDic => _stationRecipeDic;

        private string filepath = $"{PF.Core.Constants.ConstGlobalParam.ConfigPath}\\StationRecipe.json";


        public virtual Task<bool> ChangedStationRecipe(string StationName, T RecipeParam, CancellationToken token = default)
        {
            if (_stationRecipeDic.ContainsKey(StationName))
            {
                _stationRecipeDic[StationName] = RecipeParam;
            }
            else
            {
                _stationRecipeDic.TryAdd(StationName, RecipeParam);
            }
            return Task.FromResult(true);
        }

        public virtual Task<T> GetStationRecipe(string StationName, CancellationToken token = default)
        {
            if (_stationRecipeDic.TryGetValue(StationName, out T recipeParam))
            {
                return Task.FromResult(recipeParam);
            }
            else
            {
                return null;
            }
        }

        public Task<bool> WriteRecipeManger()
        {
            string str = System.Text.Json.JsonSerializer.Serialize(StationRecipeDic);
            File.WriteAllText(filepath, str);
            return Task.FromResult(true);
        }
    }
}
