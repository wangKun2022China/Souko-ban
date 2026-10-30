using System;
using System.Collections.Generic;

namespace SokoBan.Persistence
{
    // 存档数据模型（纯 DTO，供 JsonUtility 序列化；读写逻辑见 SaveManager）。
    [Serializable] public class CustomLevel { public string id; public string name; public string[] rows; public string[] links; public string[] tips; }
    [Serializable] public class ProgressEntry { public string id; public bool completed; public long at; }
    [Serializable] public class Settings { public bool sound = true; public bool deadlockHint = true; }

    [Serializable]
    public class SaveFile
    {
        public List<CustomLevel> levels = new List<CustomLevel>();
        public List<ProgressEntry> progress = new List<ProgressEntry>();
        public Settings settings = new Settings();
    }
}
