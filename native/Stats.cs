using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace AlienBridge {
  // Reconstruct earned achievements when opening an existing save.
  static class Stats {
    static bool Known(string word) {
      var dict = Global.savedata.dict;
      return dict.TryGetValue(word, out var note) && note.Trim() != "" && note != "?";
    }

    public static JObject Read() {
      if (Global.savedata == null || Global.alien_text == null || Global.quiz_supply == null)
        return null;
      var words = Global.alien_text.Keys.Where(k => !Global.alien_text[k].category.Contains("token")).ToList();
      int known = words.Count(Known);
      var earned = new List<string>();
      void Add(string id, bool got) { if (got) earned.Add(id); }
      for (int i = 0; i < Global.chapters.Count; i++) Add("C" + (i + 1), Global.GetWin(Global.chapters[i]));
      for (int i = 0; i < Global.chapters2.Count; i++) Add("S" + (i + 1), Global.GetWin(Global.chapters2[i]));
      for (int i = 0; i < Global.gamename.Count; i++) Add("G" + (i + 1), Global.GetWin(Global.gamename[i]));
      Add("END", Global.GetWin("last riddle"));
      Add("ALL", earned.Count == Global.chapters.Count + Global.chapters2.Count + Global.gamename.Count + 1);
      Add("W1", known >= 7);
      Add("W2", known >= 49);
      Add("W3", known >= 343);
      Add("W4", known == words.Count);
      Add("QUIT", Known("end"));
      Add("JOKE", Known("artist") && Known("emperor") && Global.savedata.dict["artist"].Trim() == Global.savedata.dict["emperor"].Trim());
      int total = Global.chapters.Count + Global.chapters2.Count + Global.gamename.Count + 8;
      return AchievementLedger.Correct(new JObject {
        ["achievements"] = new JArray(earned.Count, total),
        ["earned"] = new JArray(earned),
      });
    }
  }
}
