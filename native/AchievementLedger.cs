using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace AlienBridge {
  // Persist earned achievements per save slot.
  public static class AchievementLedger {
    static readonly Dictionary<string, HashSet<string>> histories = new Dictionary<string, HashSet<string>>();
    public static void Unlock(string id) {
      var stats = new JObject { ["earned"] = new JArray(id), ["achievements"] = new JArray(1, 29) };
      Correct(stats);
    }

    public static JObject Apply(JObject stats, HashSet<string> earned) {
      if (stats == null) return null;
      earned.UnionWith((stats["earned"] as JArray ?? new JArray()).Values<string>());
      stats["earned"] = new JArray(earned.OrderBy(x => x, StringComparer.Ordinal));
      int total = (int?)stats["achievements"]?[1] ?? 29;
      stats["achievements"] = new JArray(earned.Count, total);
      return stats;
    }

    public static JObject Correct(JObject stats) {
      if (stats == null || Global.globalData == null) return stats;
      var save = Environment.GetEnvironmentVariable("ALIEN_SAVE_DIR");
      if (string.IsNullOrEmpty(save)) return stats;
      var path = Path.Combine(save, "bridge-achievements-" + Global.globalData.slot + ".json");
      lock (histories) {
        if (!histories.TryGetValue(path, out var earned)) {
          earned = new HashSet<string>();
          try {
            if (File.Exists(path)) earned.UnionWith(JArray.Parse(File.ReadAllText(path)).Values<string>());
          } catch (Exception) { /* Ignore unreadable achievement metadata. */ }
          histories[path] = earned;
        }
        int before = earned.Count;
        Apply(stats, earned);
        if (earned.Count != before) {
          try {
            var temp = path + ".tmp";
            File.WriteAllText(temp, new JArray(earned.OrderBy(x => x, StringComparer.Ordinal)).ToString());
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
          } catch (Exception) { /* Keep the in-memory ledger if persistence fails. */ }
        }
        return stats;
      }
    }
  }
}
