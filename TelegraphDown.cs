// 读取链接
// 下载网页
// 提取图片链接
// 下载并整理图片

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace MyFirefoxTabs
{
    public class TelegraphDown
    {
        private static readonly HttpClient _httpClient;
        private static readonly string _savePathBase;

        private TelegraphDownLogger logger;
        private string saveName;

        public string Target { get; }
        public string TaskName => saveName;
        public string SavePath => Path.Combine(_savePathBase, nameof(TelegraphDown), saveName);
        public string Log => logger == null ? string.Empty : logger.Log;

        static TelegraphDown()
        {
            _savePathBase = Path.GetDirectoryName(Environment.ProcessPath) ?? Directory.GetCurrentDirectory();
            _httpClient = new HttpClient();
        }

        public TelegraphDown(string target)
        {
            Target = target;
            saveName = $"TelegraphDown_{DateTime.Now:yyyyMMdd}@{Guid.NewGuid():N}";
        }

        public async Task StartProcess()
        {
            logger = new TelegraphDownLogger();

            try
            {
                // Download HTML.
                logger.Information("Downloading HTML => " + Target);
                var indexFileByte = await HttpHelper.SendRequest(Target).ConfigureAwait(false);
                var indexContent = await indexFileByte.HttpContent2Text().ConfigureAwait(false);

                // Get page title.
                int titleStart = indexContent.IndexOf("<title>");
                int titleEnd = indexContent.IndexOf("</title>");
                string title = indexContent.Substring(titleStart + 7, titleEnd - titleStart - 7).Trim();
                if (title.EndsWith(" – Telegraph"))
                { title = title.Substring(0, title.Length - 12); }

                saveName = title
                    .Replace('<', '＜')
                    .Replace('>', '＞')
                    .Replace(':', '：')
                    .Replace('/', '／')
                    .Replace('\\', '＼')
                    .Replace('|', '｜')
                    .Replace('?', '？')
                    //.Replace('*', '＊')
                    .Replace('*', '×')
                    ;
                logger.Information("Obtained save name => " + saveName);

                // - Only img tags inside article tags are required.
                int articleStart, articleEnd;
                articleStart = indexContent.IndexOf("<article"); //<article id=\"_tl_editor\"
                articleEnd = articleStart > -1 ? indexContent.IndexOf("</article>", articleStart) : -1;
                if (articleStart == -1 || articleEnd == -1)
                {
                    logger.Error("No article tags found.");
                    logger.Complete();
                    return;
                }

                // Get all img tags and get their src attribute value.
                List<string> imgs = new List<string>();
                string articleContent = indexContent.Substring(articleStart, articleEnd - articleStart);
                int findNext = articleContent.IndexOf("src=\"", StringComparison.OrdinalIgnoreCase);
                int findNextEnd;
                while (findNext > -1)
                {
                    findNextEnd = articleContent.IndexOf("\"", findNext + 5, StringComparison.OrdinalIgnoreCase);
                    if (findNextEnd == -1)
                        break;

                    string findStr = articleContent.Substring(findNext + 5, findNextEnd - findNext - 5);
                    if (findStr.StartsWith('/'))
                    { findStr = "https://telegra.ph" + findStr; }

                    imgs.Add(findStr);
                    findNext = articleContent.IndexOf("src=\"", findNextEnd + 1, StringComparison.OrdinalIgnoreCase);
                }
                logger.Information(imgs.Count + " image(s) detected.");

                if (imgs.Count == 0)
                {
                    logger.Warning("No files to download.");
                    logger.Complete();
                    return;
                }

                // If it has already been downloaded, skip it.
                if (Directory.Exists(SavePath) && Directory.GetFiles(SavePath).Length >= imgs.Count)
                {
                    logger.Warning("Already downloaded.");
                    logger.Complete();
                    return;
                }

                // Download image.
                Directory.CreateDirectory(SavePath);
                string imgExt = Path.GetExtension(imgs[0]);
                for (int i = 0; i < imgs.Count; i++)
                {
                    string fileName = (i + 1).ToString().PadLeft(5, '0') + imgExt;
                    logger.Information($"Downloading {fileName}");

                    // If the download fails, it will restart.
                    // But if the number of retries is exhausted, the TelegraphDown task will be cancelled.
                    int retry = 11;
                    while (true)
                    {
                        try
                        {
                            var imgContent = await HttpHelper.SendRequest(imgs[i]).ConfigureAwait(false);
                            await imgContent.HttpContent2File(Path.Combine(SavePath, fileName)).ConfigureAwait(false);
                            break;
                        }
                        catch (Exception ex)
                        {
                            string exMsg = ex is HttpRequestException && ex.InnerException != null
                                ? $"{ex.Message} => {ex.InnerException.Message}" : ex.Message;

                            retry--;
                            if (retry <= 0)
                            {
                                logger.Warning($"Failed to download {fileName} => {exMsg}");
                                logger.Error("Download aborted. Cleaning up...");
                                Directory.Delete(SavePath, true);

                                logger.Complete();
                                return;
                            }

                            logger.Warning($"Failed to download {fileName} ({exMsg}). Retrying... ({retry} times remaining)");
                        }
                    }
                }

                logger.Complete();
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                logger.Complete();
                return;
            }

        }


        /**/

        private class TelegraphDownLogger
        {
            private static readonly FileStream _logFile;

            private readonly string loggerId;
            private readonly StringBuilder logs;

            private bool completed;

            public string Log => logs.ToString();

            static TelegraphDownLogger()
            {
                _logFile = new FileStream(
                    Path.Combine(_savePathBase, $"TelegraphDown_{DateTime.Now:yyyyMMddHHmmss}.log"),
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.Read);
            }

            public TelegraphDownLogger()
            {
                loggerId = "TelegraphDown_" + Guid.NewGuid().ToString("N");
                logs = new StringBuilder(loggerId);
            }

            public void Information(string message) => WriteLog(message);

            public void Warning(string message)
            {
                WriteLog("WARNING: " + message);
            }

            public void Error(string message)
            {
                WriteLog("ERROR: " + message);
            }

            public void Complete()
            {
                if (completed)
                    return;

                WriteLog("Processed.");
                logs.AppendLine();
                completed = true;

                lock (_logFile)
                {
                    _logFile.Write(Encoding.UTF8.GetBytes(logs.ToString()));
                    _logFile.Flush();
                }
            }

            private void WriteLog(string message)
            {
                if (completed)
                    return;

                logs.AppendLine("    |- " + message);
            }
        }

        /**/
    }
}

