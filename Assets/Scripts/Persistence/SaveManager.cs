using System;
using System.Collections.Generic;
using UnityEngine;

namespace SokoBan.Persistence
{
    // JSON 存档（逐项对照 storage.js；localStorage → persistentDataPath 文件，异常降级内存）。
    public static class SaveManager
    {
        private static SaveFile _data;
        private static string FilePath => System.IO.Path.Combine(Application.persistentDataPath, "sokoban_save.json");

        private static SaveFile Data
        {
            get
            {
                if (_data == null) Load();
                return _data;
            }
        }

        private static void Load()
        {
            _data = new SaveFile();
            try
            {
                if (System.IO.File.Exists(FilePath))
                {
                    var s = JsonUtility.FromJson<SaveFile>(System.IO.File.ReadAllText(FilePath));
                    if (s != null) _data = s;
                }
            }
            catch { /* 降级为内存默认 */ }
        }

        private static void Persist()
        {
            try { System.IO.File.WriteAllText(FilePath, JsonUtility.ToJson(Data)); } catch { }
        }

        /* --- 自定义关卡 --- */
        public static List<CustomLevel> GetLevels() => Data.levels;

        public static CustomLevel SaveLevel(CustomLevel lv)
        {
            int i = Data.levels.FindIndex(x => x.id == lv.id);
            if (i >= 0) Data.levels[i] = lv; else Data.levels.Add(lv);
            Persist();
            return lv;
        }

        public static CustomLevel GetLevel(string id) => Data.levels.Find(x => x.id == id);

        // 删除自定义关卡，并同步清理其通关进度。
        public static bool DeleteLevel(string id)
        {
            int i = Data.levels.FindIndex(x => x.id == id);
            if (i < 0) return false;
            Data.levels.RemoveAt(i);
            Data.progress.RemoveAll(x => x.id == id);
            Persist();
            return true;
        }

        /* --- 进度 --- */
        public static ProgressEntry GetBest(string id) => Data.progress.Find(x => x.id == id);
        public static bool IsCompleted(string id) => GetBest(id) != null;

        public static ProgressEntry RecordCompletion(string id)
        {
            var prev = GetBest(id);
            if (prev == null)
            {
                prev = new ProgressEntry { id = id };
                Data.progress.Add(prev);
            }
            prev.completed = true;
            prev.at = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Persist();
            return prev;
        }

        /* --- 设置 --- */
        public static Settings GetSettings() => Data.settings;

    }
}
