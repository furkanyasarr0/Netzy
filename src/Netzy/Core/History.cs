using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Netzy.Core
{
    internal sealed class HistoryStep
    {
        public string Key;      // metin anahtarı: "step.winsock", "kind.firewall"... (gösterirken çevrilir)
        public bool Ok;
        public string Detail;
    }

    internal sealed class HistoryEntry
    {
        public long Id;
        public DateTime Time = DateTime.Now;
        public string Kind;     // quick, deep, dns, undo, diagnose, error
        public bool Ok;
        public string Summary;
        public Metrics Before, After;
        public string Log = "";
        public List<HistoryStep> Steps = new List<HistoryStep>();
        public int StepTotal, StepOk;   // listede adımlar yüklenmeden sayıları göstermek için
    }

    /// <summary>
    /// İşlem geçmişi: %LOCALAPPDATA%\Netzy\history.db (SQLite).
    /// Windows 10/11'in kendi SQLite'ı (System32\winsqlite3.dll) kullanılır: ek DLL veya paket yok, EXE tek dosya kalır.
    /// </summary>
    internal static class History
    {
        const int Keep = 500;   // en eski kayıtlar silinir; dosya sınırsız büyümez
        internal static string DbPath = Path.Combine(Paths.Root, "history.db");

        static readonly string[] Schema =
        {
            "CREATE TABLE IF NOT EXISTS runs (id INTEGER PRIMARY KEY AUTOINCREMENT, time TEXT NOT NULL, kind TEXT NOT NULL, " +
            "ok INTEGER NOT NULL, summary TEXT, latency_before INTEGER, latency_after INTEGER, dns_before INTEGER, dns_after INTEGER, log TEXT NOT NULL)",
            "CREATE TABLE IF NOT EXISTS steps (run_id INTEGER NOT NULL, pos INTEGER NOT NULL, key TEXT NOT NULL, ok INTEGER NOT NULL, detail TEXT)",
            "CREATE INDEX IF NOT EXISTS steps_run ON steps(run_id)",
        };

        const string Columns = "id, time, kind, ok, summary, latency_before, latency_after, dns_before, dns_after, " +
                               "(SELECT COUNT(*) FROM steps WHERE run_id = runs.id), (SELECT COUNT(*) FROM steps WHERE run_id = runs.id AND ok = 1)";

        static Sqlite Open()
        {
            var db = Sqlite.Open(DbPath);
            foreach (var sql in Schema) db.Exec(sql);
            return db;
        }

        static bool IsDbError(Exception e) =>
            e is SqliteException || e is DllNotFoundException || e is EntryPointNotFoundException || e is IOException || e is UnauthorizedAccessException;

        /// <summary>Kaydı ekler ve numarasını döner. Veritabanı kullanılamazsa kayıt kaybolmasın diye metin dosyasına yazar ve null döner.</summary>
        public static long? Add(HistoryEntry e)
        {
            try
            {
                using (var db = Open())
                {
                    db.Exec("BEGIN");
                    db.Exec("INSERT INTO runs (time, kind, ok, summary, latency_before, latency_after, dns_before, dns_after, log) VALUES (?,?,?,?,?,?,?,?,?)",
                        e.Time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), e.Kind, e.Ok, e.Summary,
                        e.Before?.LatencyMs, e.After?.LatencyMs, e.Before?.DnsMs, e.After?.DnsMs, e.Log ?? "");
                    long id = db.LastInsertId;
                    for (int i = 0; i < e.Steps.Count; i++)
                        db.Exec("INSERT INTO steps (run_id, pos, key, ok, detail) VALUES (?,?,?,?,?)", id, i, e.Steps[i].Key, e.Steps[i].Ok, e.Steps[i].Detail);
                    db.Exec("DELETE FROM steps WHERE run_id <= ?", id - Keep);
                    db.Exec("DELETE FROM runs WHERE id <= ?", id - Keep);
                    db.Exec("COMMIT");   // bağlantı COMMIT'ten önce kapanırsa SQLite yarım kaydı geri alır
                    return id;
                }
            }
            catch (Exception ex) when (IsDbError(ex))
            {
                SaveAsText(e, ex);
                return null;
            }
        }

        /// <summary>Tüm kayıtlar, en yeni önce (ayrıntılı kayıt ve adımlar olmadan).</summary>
        public static List<HistoryEntry> List()
        {
            using (var db = Open())
                return db.Query($"SELECT {Columns} FROM runs ORDER BY id DESC").Select(FromRow).ToList();
        }

        /// <summary>Tek kaydı ayrıntılı kayıt ve adımlarıyla döner.</summary>
        public static HistoryEntry Get(long id)
        {
            using (var db = Open())
            {
                var row = db.Query($"SELECT {Columns}, log FROM runs WHERE id = ?", id).FirstOrDefault();
                if (row == null) return null;
                var e = FromRow(row);
                e.Log = (string)row[11] ?? "";
                e.Steps = db.Query("SELECT key, ok, detail FROM steps WHERE run_id = ? ORDER BY pos", id)
                    .Select(r => new HistoryStep { Key = (string)r[0], Ok = (long)r[1] == 1, Detail = (string)r[2] })
                    .ToList();
                return e;
            }
        }

        public static void Clear()
        {
            using (var db = Open())
            {
                db.Exec("DELETE FROM steps");
                db.Exec("DELETE FROM runs");
                db.Exec("VACUUM");   // silinen kayıtların dosyada kalmaması için
            }
        }

        static HistoryEntry FromRow(object[] r) => new HistoryEntry
        {
            Id = (long)r[0],
            Time = DateTime.ParseExact((string)r[1], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            Kind = (string)r[2],
            Ok = (long)r[3] == 1,
            Summary = (string)r[4],
            Before = new Metrics { LatencyMs = ToInt(r[5]), DnsMs = ToInt(r[7]) },
            After = new Metrics { LatencyMs = ToInt(r[6]), DnsMs = ToInt(r[8]) },
            StepTotal = (int)(long)r[9],
            StepOk = (int)(long)r[10],
        };

        static int? ToInt(object o) => o == null ? (int?)null : (int)(long)o;

        static void SaveAsText(HistoryEntry e, Exception reason)
        {
            try
            {
                Directory.CreateDirectory(Paths.Logs);
                var text = $"[history database unavailable: {reason.Message}]\n{e.Kind} ok={e.Ok} {e.Summary}\n" +
                           string.Join("\n", e.Steps.Select(s => $"{(s.Ok ? "OK " : "ERR")} {s.Key} {s.Detail}")) + "\n\n" + e.Log;
                File.WriteAllText(Path.Combine(Paths.Logs, $"{e.Time:yyyyMMdd-HHmmss}-{e.Kind}.txt"), text, Encoding.UTF8);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    internal sealed class SqliteException : Exception
    {
        public SqliteException(string message) : base(message) { }
    }

    /// <summary>Windows'un yerleşik SQLite kütüphanesi (winsqlite3.dll) için en küçük sarmalayıcı.</summary>
    internal sealed class Sqlite : IDisposable
    {
        const string Dll = "winsqlite3.dll";   // winsqlite3 x86'da stdcall kullanır; DllImport'un varsayılanı da budur
        const int OK = 0, ROW = 100, DONE = 101, INTEGER = 1, NULL = 5;
        static readonly IntPtr Transient = new IntPtr(-1);   // SQLITE_TRANSIENT: metin bağlanırken kopyalanır

        [DllImport(Dll, CharSet = CharSet.Unicode)] static extern int sqlite3_open16(string filename, out IntPtr db);
        [DllImport(Dll)] static extern int sqlite3_close_v2(IntPtr db);
        [DllImport(Dll)] static extern int sqlite3_busy_timeout(IntPtr db, int ms);
        [DllImport(Dll, CharSet = CharSet.Unicode)] static extern int sqlite3_prepare16_v2(IntPtr db, string sql, int nBytes, out IntPtr stmt, IntPtr tail);
        [DllImport(Dll)] static extern int sqlite3_step(IntPtr stmt);
        [DllImport(Dll)] static extern int sqlite3_finalize(IntPtr stmt);
        [DllImport(Dll)] static extern int sqlite3_bind_null(IntPtr stmt, int index);
        [DllImport(Dll)] static extern int sqlite3_bind_int64(IntPtr stmt, int index, long value);
        [DllImport(Dll, CharSet = CharSet.Unicode)] static extern int sqlite3_bind_text16(IntPtr stmt, int index, string value, int nBytes, IntPtr destructor);
        [DllImport(Dll)] static extern int sqlite3_column_count(IntPtr stmt);
        [DllImport(Dll)] static extern int sqlite3_column_type(IntPtr stmt, int col);
        [DllImport(Dll)] static extern long sqlite3_column_int64(IntPtr stmt, int col);
        [DllImport(Dll)] static extern IntPtr sqlite3_column_text16(IntPtr stmt, int col);
        [DllImport(Dll)] static extern long sqlite3_last_insert_rowid(IntPtr db);
        [DllImport(Dll)] static extern IntPtr sqlite3_errmsg16(IntPtr db);

        IntPtr _db;

        public static Sqlite Open(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var s = new Sqlite();
            if (sqlite3_open16(path, out s._db) != OK)
            {
                var message = s.Error();
                s.Dispose();
                throw new SqliteException("open failed: " + message);
            }
            sqlite3_busy_timeout(s._db, 3000);   // aynı anda iki yazma olursa beklesin, hata vermesin
            return s;
        }

        public long LastInsertId => sqlite3_last_insert_rowid(_db);

        public void Exec(string sql, params object[] args)
        {
            var stmt = Prepare(sql, args);
            try
            {
                int rc;
                while ((rc = sqlite3_step(stmt)) == ROW) { }
                if (rc != DONE) throw new SqliteException(Error());
            }
            finally { sqlite3_finalize(stmt); }
        }

        public List<object[]> Query(string sql, params object[] args)
        {
            var rows = new List<object[]>();
            var stmt = Prepare(sql, args);
            try
            {
                int n = sqlite3_column_count(stmt), rc;
                while ((rc = sqlite3_step(stmt)) == ROW)
                {
                    var row = new object[n];
                    for (int i = 0; i < n; i++)
                    {
                        int type = sqlite3_column_type(stmt, i);
                        row[i] = type == NULL ? null
                            : type == INTEGER ? (object)sqlite3_column_int64(stmt, i)
                            : Marshal.PtrToStringUni(sqlite3_column_text16(stmt, i));
                    }
                    rows.Add(row);
                }
                if (rc != DONE) throw new SqliteException(Error());
            }
            finally { sqlite3_finalize(stmt); }
            return rows;
        }

        IntPtr Prepare(string sql, object[] args)
        {
            if (sqlite3_prepare16_v2(_db, sql, -1, out var stmt, IntPtr.Zero) != OK) throw new SqliteException(Error());
            for (int i = 0; i < args.Length; i++)
            {
                int rc;
                switch (args[i])
                {
                    case null: rc = sqlite3_bind_null(stmt, i + 1); break;
                    case string text: rc = sqlite3_bind_text16(stmt, i + 1, text, -1, Transient); break;
                    case bool b: rc = sqlite3_bind_int64(stmt, i + 1, b ? 1 : 0); break;
                    default: rc = sqlite3_bind_int64(stmt, i + 1, Convert.ToInt64(args[i], CultureInfo.InvariantCulture)); break;
                }
                if (rc != OK)
                {
                    sqlite3_finalize(stmt);
                    throw new SqliteException(Error());
                }
            }
            return stmt;
        }

        string Error() => _db == IntPtr.Zero ? "unknown" : Marshal.PtrToStringUni(sqlite3_errmsg16(_db));

        public void Dispose()
        {
            if (_db == IntPtr.Zero) return;
            sqlite3_close_v2(_db);
            _db = IntPtr.Zero;
        }
    }
}
