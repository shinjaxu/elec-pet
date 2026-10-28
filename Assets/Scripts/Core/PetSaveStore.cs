using System;
using System.IO;

namespace ElecPet.Core
{
    // 编解码由运行时提供；同目录替换避免半份文件成为正式存档。
    public sealed class PetSaveStore
    {
        private readonly string directory, primary, backup;
        private readonly Func<string, PetSaveData> decode;
        private readonly Func<PetSaveData, string> encode;
        private bool primaryValid;
        public bool CanSave { get; private set; }
        public Action<string, Exception> Report;

        public PetSaveStore(string directory, Func<string, PetSaveData> decode, Func<PetSaveData, string> encode)
        {
            this.directory = directory; this.decode = decode; this.encode = encode;
            primary = Path.Combine(directory, "pet.json");
            backup = Path.Combine(directory, "pet.backup.json");
        }

        public PetSaveData Load(Func<PetSaveData> migrate)
        {
            CanSave = true;
            primaryValid = false;
            if (!File.Exists(primary) && !File.Exists(backup)) return migrate();
            PetSaveData data;
            if (TryRead(primary, out data)) { primaryValid = true; return data; }
            // 新版本不能回退读取旧备份，否则会在下次保存时丢失新版本进度。
            if (!CanSave) return PetSaveData.Default();
            if (TryRead(backup, out data)) return data;
            return PetSaveData.Default();
        }

        private bool TryRead(string path, out PetSaveData data)
        {
            data = null;
            if (!File.Exists(path)) return false;
            try
            {
                data = decode(File.ReadAllText(path));
                if (data != null && data.Version > 1)
                {
                    CanSave = false;
                    Notify("存档版本较新，本次禁用写入以保留原进度", new InvalidDataException("不支持的存档版本"));
                    return false;
                }
                if (data == null || !data.IsValid()) throw new InvalidDataException("存档字段或范围无效");
                return true;
            }
            catch (Exception exception)
            {
                // 保留损坏原件后才能接受默认状态；保留失败时禁止后续覆盖。
                try { File.Copy(path, path + ".corrupt-" + Guid.NewGuid().ToString("N")); }
                catch (Exception archiveError) { CanSave = false; Notify("损坏存档保留失败，本次禁用写入", archiveError); }
                Notify("读取存档失败，将尝试备份或默认状态", exception);
                return false;
            }
        }

        public void Save(PetSaveData data)
        {
            if (!CanSave) throw new InvalidOperationException("当前存档禁止覆盖");
            if (data == null || !data.IsValid()) throw new InvalidDataException("待保存数据无效");
            Directory.CreateDirectory(directory);
            string temporary = primary + ".tmp";
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(encode(data));
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (File.Exists(primary))
                File.Replace(temporary, primary, primaryValid ? backup : null);
            else File.Move(temporary, primary);
            primaryValid = true;
        }

        private void Notify(string stage, Exception exception) { if (Report != null) Report(stage, exception); }
    }
}
