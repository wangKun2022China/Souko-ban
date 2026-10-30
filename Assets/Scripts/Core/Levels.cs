using System;
using System.Collections.Generic;
using UnityEngine;

namespace SokoBan.Core
{
    // 内置经典关卡的只读注册表。数据存于文本资产 Assets/Resources/SokoBan/levels.json，
    /// 与自定义关卡同用 rows/links 文本格式，加关改关无需重编译。
    public static class Levels
    {
        public class BuiltInLevel
        {
            public string Id;
            public string Name;
            public int ParMoves;
            public string[] Rows;
            public string[] Links;
            public string[] Tips;
        }

        [Serializable] private class LevelData
        {
            public string id;
            public string name;
            public int parMoves;
            public string[] rows;
            public string[] links;
            public string[] tips;
        }

        [Serializable] private class LevelCollection
        {
            public LevelData[] levels;
        }

        private const string ResourcePath = "SokoBan/levels";
        private static IReadOnlyList<BuiltInLevel> _all;

        public static IReadOnlyList<BuiltInLevel> All
        {
            get
            {
                if (_all == null) Load();
                return _all;
            }
        }

        private static void Load()
        {
            var ta = Resources.Load<TextAsset>(ResourcePath);
            if (ta == null)
                throw new InvalidOperationException("找不到内置关卡数据 Assets/Resources/SokoBan/levels.json");
            var data = JsonUtility.FromJson<LevelCollection>(ta.text);
            var list = new List<BuiltInLevel>();
            if (data != null && data.levels != null)
                foreach (var lv in data.levels)
                    list.Add(new BuiltInLevel
                    {
                        Id = lv.id,
                        Name = lv.name,
                        ParMoves = lv.parMoves,
                        Rows = lv.rows,
                        Links = lv.links ?? new string[0],
                        Tips = lv.tips ?? new string[0],
                    });
            _all = list;
        }
    }
}
