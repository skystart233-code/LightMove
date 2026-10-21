using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace grapher.CurveEditing
{
    public sealed class UserCurvePreset
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool Linked { get; set; }
        public List<CurvePoint> X { get; set; }
        public List<CurvePoint> Y { get; set; }
        public UserCurvePreset Copy() => new UserCurvePreset { Id = Id, Name = Name, Linked = Linked, X = new CurveModel(X).Points, Y = new CurveModel(Y).Points };
    }

    public sealed class UserPresetStore
    {
        private readonly string path;
        private List<UserCurvePreset> items;
        public IReadOnlyList<UserCurvePreset> Items => items.Select(p => p.Copy()).ToList().AsReadOnly();
        public UserPresetStore(string path)
        {
            this.path = path;
            items = File.Exists(path) ? JsonConvert.DeserializeObject<List<UserCurvePreset>>(File.ReadAllText(path)) : new List<UserCurvePreset>();
            if (items == null || items.Count > 100) throw new IOException("预设文件无法读取，原文件已保留。");
            foreach (var item in items) Validate(item);
            if (items.Select(p=>p.Id).Distinct().Count()!=items.Count) throw new IOException("预设编号重复，原文件已保留。");
        }
        private static void Validate(UserCurvePreset preset)
        {
            if (preset == null || string.IsNullOrWhiteSpace(preset.Id) || string.IsNullOrWhiteSpace(preset.Name) || preset.Name.Length > 40 || preset.Name.Any(char.IsControl))
                throw new ArgumentException("请使用 1 至 40 个字符的预设名称。");
            new CurveModel(preset.X); new CurveModel(preset.Y);
        }
        public UserCurvePreset Add(UserCurvePreset preset)
        {
            if (items.Count >= 100) throw new IOException("已保存 100 个预设，请先删除不需要的方案。");
            var copy = preset.Copy(); copy.Id = Guid.NewGuid().ToString("N"); copy.Name = (copy.Name ?? "").Trim(); Validate(copy);
            string original = copy.Name; int suffix = 2;
            while (items.Any(p=>string.Equals(p.Name,copy.Name,StringComparison.OrdinalIgnoreCase)))
            { string tail=" ("+(suffix++).ToString()+")"; copy.Name=original.Substring(0,Math.Min(original.Length,40-tail.Length))+tail; }
            Commit(new[] { copy }.Concat(items).ToList()); return copy.Copy();
        }
        public UserCurvePreset Delete(string id)
        {
            var removed = items.Single(p=>p.Id==id).Copy(); Commit(items.Where(p=>p.Id!=id).ToList()); return removed;
        }
        public void Restore(UserCurvePreset preset)
        {
            Validate(preset);
            if (items.Count >= 100 || items.Any(p=>p.Id==preset.Id)) throw new IOException("无法恢复此预设。");
            Commit(new[] { preset.Copy() }.Concat(items).ToList());
        }
        private void Commit(List<UserCurvePreset> next)
        {
            string temporary=path+".tmp";
            File.WriteAllText(temporary,JsonConvert.SerializeObject(next,Formatting.Indented));
            if(File.Exists(path)) File.Replace(temporary,path,null); else File.Move(temporary,path);
            items=next;
        }
    }
}
