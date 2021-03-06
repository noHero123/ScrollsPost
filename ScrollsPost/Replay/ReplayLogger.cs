using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Net;
using System.Text;
using JsonFx.Json;

namespace ScrollsPost {
    public class ReplayLogger : IOkCancelCallback, ICommListener {
        private ScrollsPost.Mod mod;
        public String replayFolder;
        public String uploadCachePath;

        private String replayPath;
        private String currentVersion;
        private double lastMessage;
        private Boolean inGame;
        private Boolean enabled;
        private StreamWriter sw;

        public ReplayLogger(ScrollsPost.Mod mod) {
            this.mod = mod;
            App.Communicator.addListener(this);

            replayFolder = this.mod.OwnFolder() + Path.DirectorySeparatorChar + "replays";
            if( !Directory.Exists(replayFolder + Path.DirectorySeparatorChar) ) {
                Directory.CreateDirectory(replayFolder + Path.DirectorySeparatorChar);
            }

            uploadCachePath = replayFolder + Path.DirectorySeparatorChar + "upload-cache";
        }

        public void handleMessage(Message msg) {
            // Check if we should start recording
            if( msg is BattleRedirectMessage ) {
                enabled = true;
                return;
            
            // Grab version for metadata
            } else if( msg is ServerInfoMessage ) {
                currentVersion = (msg as ServerInfoMessage).version;

            // If you disconnect mid game, it'll have to trigger this on reconnect
            // so only do this part if we flagged it as not being in a game, which means it really ended.
            } else if( !inGame && enabled && msg is ProfileInfoMessage ) {
                if( mod.config.GetString("replay").Equals("ask") ) {
                    App.Popups.ShowOkCancel(this, "replay", "Upload Replay?", "Do you want this replay to be uploaded to ScrollsPost.com?", "Yes", "No");
                }

                enabled = false;
                            
            // Not logging yet
            } else if( !enabled ) {
                return;
            }

            // Initial game start
            if( msg is GameInfoMessage ) {
                if( inGame )
                    return;

                inGame = true;
                lastMessage = mod.TimeSinceEpoch();

                GameInfoMessage info = (GameInfoMessage) msg;

                Dictionary<String, object> metadata = new Dictionary<String, object>();
                metadata["perspective"] = info.color == TileColor.white ? "white" : "black";
                metadata["white-id"] = info.getPlayerProfileId(TileColor.white);
                metadata["black-id"] = info.getPlayerProfileId(TileColor.black);
                metadata["white-name"] = info.getPlayerName(TileColor.white);
                metadata["black-name"] = info.getPlayerName(TileColor.black);
                metadata["deck"] = "dont know";
                metadata["game-id"] = Convert.ToDouble(info.gameId);
                metadata["winner"] = "SPWINNERSP";
                metadata["played-at"] = (int) lastMessage;
                metadata["version"] = currentVersion;

                replayPath = replayFolder + Path.DirectorySeparatorChar + String.Format("{0}-{1}.spr", metadata["game-id"], metadata["perspective"]);

                // Store metadata for easier parsing
                int buffer = mod.config.ContainsKey("buffer") ? mod.config.GetInt("buffer") : 4096;
                sw = new StreamWriter(replayPath, true, Encoding.UTF8, buffer);
                sw.WriteLine(String.Format("metadata|{0}", new JsonWriter().Write(metadata)));

            // Junk we can ignore
            } else if( msg is BattleRejoinMessage || msg is FailMessage || msg is OkMessage ) {
                return;
            }

            if( !inGame )
                return;

            double epoch = mod.TimeSinceEpoch();
            sw.WriteLine(String.Format("elapsed|{0}|{1}", Math.Round(epoch - lastMessage, 2), msg.getRawText().Replace("\n", "")));

            // Game over
            if( msg is NewEffectsMessage && msg.getRawText().Contains("EndGame") ) {
                inGame = false;

                // Finish off
                sw.Flush();
                sw.Close();
                sw = null;

                // Bit of a hack, need to improve somehow
                String contents = File.ReadAllText(replayPath);
                contents = contents.Replace("SPWINNERSP", msg.getRawText().Contains("winner\":\"white\"") ? "white" : "black");
                File.WriteAllText(replayPath, contents);

                // Start uploading immediately since we don't need to wait for anyone
                if( mod.config.GetString("replay").Equals("auto") ) {
                    new Thread(new ThreadStart(Upload)).Start();
                } else {
                    LogNotUploaded(replayPath);
                }
            }

            lastMessage = epoch;
        }

        public void onConnect(OnConnectData ocd)
        {
            return;
        }

        // Handle replay uploading
        public void PopupCancel(String type) {
            mod.SendMessage("Replay will not be uploaded, you can always manually upload it later if you change your mind. Go to /sp -> Replay List to manually upload.");
        }

        public void PopupOk(String type) {
            mod.SendMessage("Replay is being uploaded...");
            new Thread(new ThreadStart(Upload)).Start();
        }

        private void Upload() {
            Upload(replayPath);
        }

        private void LogNotUploaded(String path) {
            using( StreamWriter sw = File.AppendText(uploadCachePath) ) {
                sw.WriteLine(Path.GetFileName(path));
            }
        }

        private void LogUploaded(String path) {
            if( !File.Exists(uploadCachePath) ) {
                return;
            }

            String name = Path.GetFileName(path);

            var lines = new List<String>();
            using( StreamReader sw = new StreamReader(uploadCachePath) ) {
                while( sw.Peek() > 0 ) {
                String line = sw.ReadLine();
                    if( !line.Equals(name) ) {
                        lines.Add(line);
                    }
                }
            }

            File.WriteAllLines(uploadCachePath, lines.ToArray());
        }

        public Dictionary<String, object> Upload(String path) {
            // Setup
            String boundary = String.Format("---------------------------{0}", (int)mod.TimeSinceEpoch());
            byte[] boundaryBytes = Encoding.ASCII.GetBytes(String.Format("\r\n--{0}\r\n", boundary));

            HttpWebRequest wr = (HttpWebRequest) WebRequest.Create(mod.apiURL + "/v1/replays");
            wr.Method = "POST";
            wr.ContentType = String.Format("multipart/form-data; boundary={0}", boundary);

            // Start the boundary off
            using( Stream stream = wr.GetRequestStream() ) {
                stream.Write(boundaryBytes, 0, boundaryBytes.Length);

                // File info
                String field = String.Format("Content-Disposition: form-data; name=\"{0}\"; filename=\"{1}\"\r\nContent-Type: {2}\r\n\r\n", "replay", Path.GetFileName(path), "text/plain");
                byte[] bytes = Encoding.UTF8.GetBytes(field);

                stream.Write(bytes, 0, bytes.Length);

                // Write the file
                bytes = File.ReadAllBytes(path);
                stream.Write(bytes, 0, bytes.Length);

                bytes = Encoding.ASCII.GetBytes(String.Format("\r\n--{0}--\r\n", boundary));
                stream.Write(bytes, 0, bytes.Length);
            }

            try {
                using( WebResponse wres = wr.GetResponse() ) {
                    using( StreamReader rs = new StreamReader(wres.GetResponseStream()) ) {
                        String contents = rs.ReadToEnd();
                        Dictionary<String, object> response = new JsonReader().Read<Dictionary<String, object>>(contents);
                            
                        if( response.ContainsKey("url") ) {
                            mod.SendMessage(String.Format("Finished uploading replay to ScrollsPost. Can be found at {0}, or by typing /sp and going to Replay List.", (response["url"] as String).Replace("scrollspost/", "scrollspost.com/")));
                            LogUploaded(path);
                        } else if( response["error"].Equals("game_too_short") ) {
                            mod.SendMessage("Replay rejected as it was too short, must go beyond 1 round to be uploaded.");
                        } else {
                            mod.SendMessage(String.Format("Error while uploading replay ({0}), please contact us for more info at support@scrollspost.com", response["error"]));
                            LogNotUploaded(path);
                        }

                        return response;
                    }
                }

            } catch ( WebException we ) {
                LogNotUploaded(path);

                Console.WriteLine("**** ERROR {0}", we.ToString());
                mod.SendMessage(String.Format("We had an HTTP error while uploading replay {0}, contact us at support@scrollspost.com for help.", Path.GetFileName(path)));
                mod.WriteLog("Failed to sync collection", we);

                Dictionary<String, object> response = new Dictionary<String, object>();
                response["error"] = we.ToString();

                return response;
            }
        }
    }
}

