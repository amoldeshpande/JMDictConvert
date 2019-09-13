using System;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Xml.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Diagnostics;
using System.Data.SQLite;
using System.IO;
using System.Collections.Generic;

namespace JMDictConvert
{
    public partial class Form1 : Form
    {
        const string XML_LANG_NAME = "{http://www.w3.org/XML/1998/namespace}lang";

        public Form1()
        {
            InitializeComponent();
        }
        class JMDictEntry
        {
            public long sequence;
            public List<XElement> kanji_elements;
            public List<XElement> reading_elements;
            //public List<XElement> info_elements;
            public List<XElement> sense_elements;
        }
        private void button1_Click(object sender, EventArgs e)
        {
            Stopwatch stopWatch = new Stopwatch();
            OpenFileDialog ofd_xml = new OpenFileDialog();
            OpenFileDialog ofd_sql = new OpenFileDialog();
            ofd_xml.Filter = "XML Files (*.xml) | *.xml";
            ofd_sql.Filter = "SQLite 3.0 Files (*.s3db) | *.s3db";
            ofd_xml.Title = "Select JMDICT XML file";
            ofd_sql.Title = "Select SQLite DB to transform into";

            if (ofd_xml.ShowDialog() != DialogResult.OK)
            {
                return;
            }
            Cursor curr = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;
            XDocument doc = XDocument.Load(ofd_xml.FileName);
            var jmdict = from d in doc.Descendants("JMdict")
                         where !d.IsEmpty
                         select d.Value;

            Cursor.Current = curr;
            if (jmdict.Count() != 1)
            {
                throw new InvalidOperationException("Not a valid JMdict file");
            }
            textBox1.Text = "Loaded JMDICT from " + ofd_xml.FileName;


            stopWatch.Start();
            List<JMDictEntry> entries = (from ent in doc.Descendants("entry")
                                         select new JMDictEntry()
                                         {
                                             sequence = Int64.Parse(ent.Element("ent_seq").Value),
                                             kanji_elements = ent.Elements("k_ele").ToList(),
                                             reading_elements = ent.Elements("r_ele").ToList(),
                                             //info_elements = ent.Elements("info").ToList(),
                                             sense_elements = ent.Elements("sense").ToList()
                                         }).ToList();

            if (ofd_sql.ShowDialog() != DialogResult.OK)
            {
                return;
            }
            SQLiteConnection conn = new SQLiteConnection("Data Source=" + ofd_sql.FileName);
            conn.Open();
            SQLiteTransaction trans = conn.BeginTransaction();
            int i = 1;
            Debugger.Log(0, "", "Found " + entries.Count() + " entries");
            foreach (JMDictEntry jde in entries)
            {
                //                    Debugger.Log(0, "", "entry seq no " + jde.sequence);
                insertKanji(conn, jde.sequence, jde.kanji_elements);
                insertReading(conn, jde.sequence, jde.reading_elements);
                insertSense(conn, jde.sequence, jde.sense_elements);


                if (i % 5000 == 0)
                {
                    trans.Commit();
                    trans.Dispose();
                    trans = conn.BeginTransaction();
                    //logTime(s, "Transaction time (" + i + "):");
                }
                i++;
            }
            trans.Commit();
            trans.Dispose();

            textBox1.Text = entries.Count() + " entries parsed and converted in ";
            stopWatch.Stop();
            TimeSpan ts = stopWatch.Elapsed;
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds,
            ts.Milliseconds / 10);
            textBox1.Text += elapsedTime;
        }
        void logTime(Stopwatch s,string what)
        {
            s.Stop();
            TimeSpan ts = s.Elapsed;
            s.Reset();
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds,
            ts.Milliseconds / 10);
            Debugger.Log(0, "", what + ", time taken : " + elapsedTime + Environment.NewLine);
        }
        private string concatElements(List<XElement> catList,string separator)
        {
            StringBuilder catStr = new StringBuilder();
            for(int i = 0; i < catList.Count -1; i++)
            {
                catStr.Append(catList[i].Value);
                catStr.Append(separator);

            }
            catStr.Append(catList[catList.Count - 1].Value);
            return catStr.ToString();
        }
        private void insertKanji(SQLiteConnection conn, long sequenceNumber, List<XElement> kanjiElements)
        {
            int rowsUpdated = 0;
                Stopwatch s = new Stopwatch();
                s.Start();

            foreach (XElement xke in kanjiElements.ToList())
            {
                StringBuilder ke_pri = new StringBuilder();
                {
                    List<XElement> kpri = xke.Elements("ke_pri").ToList();

                    if (kpri.Count > 0)
                    {
                        for (int i = 0; i < kpri.Count - 1; i++)
                        {
                            ke_pri.Append(kpri[i].Value);
                            ke_pri.Append(",");
                        }
                        ke_pri.Append(kpri[kpri.Count - 1].Value);
                    }
                }

                StringBuilder ke_inf = new StringBuilder();
                {
                    List<XElement> kinf = xke.Elements("ke_inf").ToList();

                    if (kinf.Count > 0)
                    {
                        for (int i = 0; i < kinf.Count - 1; i++)
                        {
                            ke_inf.Append(kinf[i].Value);
                            ke_inf.Append(",");
                        }
                        ke_inf.Append(kinf[kinf.Count - 1].Value);
                    }
                }
                string s_ke_pri = ke_pri.ToString().Length == 2 ? "null" : ke_pri.ToString();
                string s_ke_inf = ke_inf.ToString().Length == 2 ? "null" : ke_inf.ToString();

                using (SQLiteCommand command = new SQLiteCommand(conn))
                {

                    command.CommandText = "INSERT INTO kanji (sequence,keb,ke_inf,ke_pri) VALUES( @seq,@keb,@ke_inf,@ke_pri)";
                    command.Parameters.AddWithValue("@seq", sequenceNumber);
                    command.Parameters.AddWithValue("@keb", xke.Element("keb").Value);
                    command.Parameters.AddWithValue("@ke_inf", s_ke_inf);
                    command.Parameters.AddWithValue("@ke_pri", s_ke_pri);

                    rowsUpdated += command.ExecuteNonQuery();
                }

            }
                //logTime(s, "Kanji command");

            //Debugger.Log(0, "", rowsUpdated + " rows inserted");
        }
        private void insertReading(SQLiteConnection conn, long sequenceNumber, List<XElement> readings)
        {
            int rowsUpdated = 0;

            Stopwatch s = new Stopwatch();
            s.Start();
            foreach (XElement xre in readings.ToList())
            {
                string re_nokanji = xre.Element("re_nokanji") != null ? xre.Element("re_nokanji").Value : null;
                if (re_nokanji == null)
                {
                    re_nokanji = "";
                }
                if (re_nokanji.Length == 0)
                {
                    re_nokanji = " ";
                }
                StringBuilder restrictions = new StringBuilder();
                {
                    List<XElement> restr = xre.Elements("re_restr").ToList();
                    if (restr.Count > 0)
                    {
                        restrictions.Append(concatElements(restr, ","));
                    }
                }
                StringBuilder info = new StringBuilder();
                {
                    List<XElement> infos = xre.Elements("re_inf").ToList();
                    if (infos.Count > 0)
                    {
                        info.Append(concatElements(infos, ","));
                    }
                }
                StringBuilder priorities = new StringBuilder();
                {
                    List<XElement> pri = xre.Elements("re_inf").ToList();
                    if (pri.Count > 0)
                    {
                        priorities.Append(concatElements(pri, ","));
                    }
                }
                string reb = xre.Element("reb").Value;
                string romaji = ConvertKanaToLatin(reb);

                using (SQLiteCommand command = new SQLiteCommand(conn))
                {
                    command.CommandText = "INSERT INTO reading (sequence,reb,re_nokanji,restrictions,info,re_pri,romaji)" +
                                                "VALUES( @seqno,@reb,@nokan,@restr,@info,@pri,@romaji)";
                    command.Parameters.AddWithValue("@seqno", sequenceNumber);
                    command.Parameters.AddWithValue("@reb",reb);
                    command.Parameters.AddWithValue("@romaji",romaji);
                    command.Parameters.AddWithValue("@nokan", re_nokanji);
                    command.Parameters.AddWithValue("@restr", restrictions);
                    command.Parameters.AddWithValue("@info", info);
                    command.Parameters.AddWithValue("@pri", priorities);

                    rowsUpdated += command.ExecuteNonQuery();
                }

            }
            //logTime(s, "reading command");
            //Debugger.Log(0, "", rowsUpdated + " rows inserted");
        }
        private void insertSense(SQLiteConnection conn, long sequenceNumber, List<XElement> senseElements)
        {
            int rowsUpdated = 0;
            long senseNumber = sequenceNumber; 
                Stopwatch s = new Stopwatch();
                s.Start();

            foreach (XElement xse in senseElements.ToList())
            {

                string stagk = "", stagr = "";

                // get kanji sense restrictions
                if (xse.Elements("stagk").Count() != 0)
                {
                    stagk =  concatElements(xse.Elements("stagk").ToList(), ",") ;
                }
                // get reading sense restrictions
                if (xse.Elements("stagr").Count() != 0)
                {
                    stagr =  concatElements(xse.Elements("stagr").ToList(), ",");
                }
                string xref = "";
                if (xse.Elements("xref").Count() != 0)
                {
                    xref =  concatElements(xse.Elements("xref").ToList(), ",");
                }
                string antonyms = "";
                if (xse.Elements("ant").Count() != 0)
                {
                    antonyms =  concatElements(xse.Elements("ant").ToList(), ",");
                }
                string part_of_speech = "";
                if (xse.Elements("pos").Count() != 0)
                {
                    part_of_speech =  concatElements(xse.Elements("pos").ToList(), ",");
                }
                string field_of_application = "";
                if (xse.Elements("field").Count() != 0)
                {
                    field_of_application =  concatElements(xse.Elements("field").ToList(), ",");
                }
                string misc = "";
                if (xse.Elements("misc").Count() != 0)
                {
                    misc =  concatElements(xse.Elements("misc").ToList(), ",");
                }
                string source_language = "";
                string source_language_word = "";
                if (xse.Elements("lsource").Count() != 0)
                {
                    if (xse.Elements("lsource").Count() != 1)
                    {
                        source_language_word =  xse.Elements("lsource").ToList()[0].Value ;
                        source_language = xse.Elements("lsource").ToList()[0].Attribute(XML_LANG_NAME).Value;
                    }
                    else
                    {
                        source_language_word =  xse.Element("lsource").Value ;
                        source_language = xse.Element("lsource").Attribute(XML_LANG_NAME).Value;
                    }
                }
                string dialect = "";
                if (xse.Elements("dial").Count() != 0)
                {
                    dialect =  concatElements(xse.Elements("dial").ToList(), ",");
                }
                if (xse.Elements("gloss").Count() != 0)
                {
                    insertGlosses(conn,sequenceNumber, senseNumber, xse.Elements("gloss").ToList());
                }



                using (SQLiteCommand command = new SQLiteCommand(conn))
                {
                    command.CommandText = "INSERT INTO sense (sequence,stagk,stagr,xref,antonym,part_of_speech," +
                                           "field_of_application,misc,source_language,source_language_word,dialect,sense_id) " +
                                           "VALUES( @seqno,@stagk,@stagr,@xref,@antonyms,@pos,@field,@misc,@lsource," +
                                           " @lsource_word,@dial,@sensnum)";
                    command.Parameters.AddWithValue("@seqno", sequenceNumber);
                    command.Parameters.AddWithValue("@stagk", stagk);
                    command.Parameters.AddWithValue("@stagr", stagr);
                    command.Parameters.AddWithValue("@xref", xref);
                    command.Parameters.AddWithValue("@antonyms", antonyms);
                    command.Parameters.AddWithValue("@field", field_of_application);
                    command.Parameters.AddWithValue("@pos", part_of_speech);
                    command.Parameters.AddWithValue("@misc", misc);
                    command.Parameters.AddWithValue("@lsource", source_language);
                    command.Parameters.AddWithValue("@lsource_word", source_language_word);
                    command.Parameters.AddWithValue("@dial", dialect);
                    command.Parameters.AddWithValue("@sensnum", senseNumber);

                    rowsUpdated += command.ExecuteNonQuery();
                }

                senseNumber++;
            }
                //logTime(s, "sense command");

        }
        private void insertGlosses(SQLiteConnection conn, long sequenceNumber, long senseNumber, List<XElement> glossElements)
        {
            int rowsUpdated = 0;
            Stopwatch s = new Stopwatch();
            s.Start();
            foreach (XElement xge in glossElements.ToList())
            {
                string meaning = xge.Value;
                string lang = xge.Attribute(XML_LANG_NAME).Value;
                string gender =  getAttribute(xge.Attribute("g_gend"),"");

                using (SQLiteCommand command = new SQLiteCommand(conn))
                {
                    command.CommandText = "INSERT INTO gloss (sequence,sense_id,gloss_lang, " +
                                                        "meaning,gender) VALUES( @seqno,@sensenum,@lang,@meaning,@gender)";
                    command.Parameters.AddWithValue("@seqno", sequenceNumber);
                    command.Parameters.AddWithValue("@sensenum", senseNumber);
                    command.Parameters.AddWithValue("@lang", lang);
                    command.Parameters.AddWithValue("@meaning", meaning);
                    command.Parameters.AddWithValue("@gender", gender);

                    rowsUpdated += command.ExecuteNonQuery();
                }

            }
            //    logTime(s, "gloss command");
        }
        private string getAttribute(XAttribute attr,string def)
        {
            if (attr == null)
            {
                return def;
            }
            return attr.Value;
        }
        /*
        string kanaToRomaji(string kanaStr)
        {
            Hashtable KanaToRomajiTable = new Hashtable() 
            {  
                //Hiragana
              {"あ","a"},  {"い","i"},   {"う","u"},   {"え","e"},  {"お", "o"},
              {"か","ka"}, {"き","ki"},  {"く","ku"},  {"け","ke"}, {"こ","ko"},
              {"が","ga"}, {"ぎ","gi"},  {"ぐ","gu"},  {"げ","ge"}, {"ご","go"},
              {"さ","sa"}, {"し","shi"}, {"す","su"},  {"せ","se"}, {"そ","so"},
              {"ざ","za"}, {"じ","ji"},  {"ず","zu"},  {"ぜ","ze"}, {"ぞ","zo"},
              {"た","ta"}, {"ち","chi"}, {"つ","tsu"}, {"て","te"}, {"と","to"},
              {"だ","da"}, {"ぢ","ji"},  {"づ","zu"},  {"で","de"}, {"ど","do"},
              {"な","na"}, {"に","ni"},  {"ぬ","nu"},  {"ね","ne"}, {"の","no"},
              {"は","ha"}, {"ひ","hi"},  {"ふ","fu"},  {"へ","he"}, {"ほ","ho"},
              {"ば","ba"}, {"び","bi"},  {"ぶ","bu"},  {"べ","be"}, {"ぼ","bo"},
              {"ぱ","pa"}, {"ぴ","pi"},  {"ぷ","pu"},  {"ぺ","pe"}, {"ぽ","po"},
              {"ま","ma"}, {"み","mi"},  {"む","mu"},  {"め","me"}, {"も","mo"},
              {"ら","ra"}, {"り","ri"},  {"る","ru"},  {"れ","re"}, {"ろ","ro"}, 
              {"や","ya"}, {"ゆ","yu"},  {"よ","yo"}, 
              {"わ","wa"}, {"ゐ","wi"},  {"ゑ","we"},  {"を","wo"},       
              {"ん","n"},    
              {"きゃ","kya"}, {"きゅ","kyu"}, {"きょ","kyo"},
              {"しゃ","sha"}, {"しゅ","shu"}, {"しょ","sho"},
              {"ちゃ","cha"}, {"ちゅ","chu"}, {"ちょ","cho"},
              {"ひゃ","hya"}, {"ひゅ","hyu"}, {"ひょ","hyo"},
              {"みゃ","mya"}, {"みゅ","myu"}, {"みょ","myo"},
              {"りゃ","rya"}, {"りゅ","ryu"}, {"りょ","ryo"},
              {"ぎゃ","gya"}, {"ぎゅ","gyu"}, {"ぎょ","gyo"},
              {"じゃ","ja"},  {"じゅ","ju"},  {"じょ","jo"},
              {"ぢゃ","ja"},  {"ぢゅ","ju"},  {"ぢょ","jo"},  
              {"にゃ","nya"}, {"にゅ","nyu"}, {"にょ","nyo"},    
              {"びゃ","bya"}, {"びゅ","byu"}, {"びょ","byo"},
              {"ぴゃ","pya"}, {"ぴゅ","pyu"}, {"ぴょ","pyo"},
              // Katakana
              {"ア","A"},  {"イ","I"},  {"ウ","U"},  {"エ","E"},  {"オ","O" },
              {"カ","KA"}, {"キ","KI"}, {"ク","KU"}, {"ケ","KE"}, {"コ","KO"}, 
              {"ガ","GA"}, {"ギ","GI"}, {"グ","GU"}, {"ゲ","GE"}, {"ゴ","GO"}, 
              {"ン","N"},  
              {"サ","SA"}, {"シ","SHI"}, {"ス","SU"}, {"セ","SE"}, {"ソ","SO"},
              {"ザ","ZA"}, {"ジ","JI"},  {"ズ","ZU"}, {"ゼ","ZE"}, {"ゾ","ZO"},      
              {"タ","TA"}, {"チ","CHI"}, {"ツ","TSU"},{"テ","TE"}, {"ト","TO"},
              {"ダ","DA"}, {"ヂ","JI"},  {"ヅ","ZU"}, {"デ","DE"}, {"ド","DO"},
              {"ナ","NA"}, {"ニ","NI"},  {"ヌ","NU"}, {"ネ","NE"}, {"ノ","NO"}, 
              {"ハ","HA"}, {"ヒ","HI"},  {"フ","FU"}, {"ヘ","HE"}, {"ホ","HO"}, 
              {"バ","BA"}, {"ビ","BI"},  {"ブ","BU"}, {"ベ","BE"}, {"ボ","BO"},
              {"パ","PA"}, {"ピ","PI"},  {"プ","PU"}, {"ペ","PE"}, {"ポ","PO"},
              {"マ","MA"}, {"ミ","MI"},  {"ム","MU"}, {"メ","ME"}, {"モ","MO"}, 
              {"ヤ","YA"}, {"ユ","YU"},  {"ヨ","YO"}, 
              {"ラ","RA"}, {"リ","RI"},  {"ル","RU"}, {"レ","RE"}, {"ロ","RO"}, 
              {"ワ","WA"}, {"ヰ","WI"},  {"ヱ","WE"}, {"ヲ","WO"},
              {"キャ","KYA"}, {"キュ","KYU"}, {"キョ","KYO"}, 
              {"シャ","SHA"}, {"シュ","SHU"}, {"ショ","SHO"},
              {"ズィ","ZI"},    
              {"チャ","CHA"}, {"チュ","CHU"}, {"チョ","CHO"}, 
              {"ヂャ","JA"},  {"ヂュ","JU"}, 
              {"ヒャ","HYA"}, {"ヒュ","HYU"}, {"ヒュ","HYU"}, {"ヒョ","HYO"},
              {"ミャ","MYA"}, {"ミュ","MYU"}, {"ミョ","MYO"}, 
              {"リャ","RYA"}, {"リュ","RYU"}, {"リョ","RYO"}, 
              {"ギャ","GYA"}, {"ギュ","GYU"}, {"ギョ","GYO"}, 
              {"ジャ","JA"},  {"ジュ","JU"},  {"ジョ","JO"}, 
              {"ニャ","NYA"}, {"ニュ","NYU"}, {"ニョ","NYO"}, 
              {"ビャ","BYA"}, {"ビュ","BYU"}, {"ビョ","BYO"}, 
              {"ピャ","PYA"}, {"ピュ","PYU"}, {"ピョ","PYO"}, 
              {"シェ","SHE"}, {"チェ","CHE"}, {"ツァ","TSA"}, {"ツィ","TWI"},{"ツェ","TSE"}, 
              {"ジェ","JE"}, 
              {"ツォ","TSO"}, {"ツィ","TSI"},  
              {"ティ","TI"},  {"ディ","DI"},  {"トゥ","TU"},  {"ドゥ","DU"}, 
              {"テュ","TYU"}, {"デュ","DYU"}, {"ヴ","VU"},
              {"ファ","FA"},  {"フィ","FI"},  {"フュ","FYU"}, {"ヴュ","VYU"}, 
              {"フェ","FE"},  {"ヴェ","VE"},  {"フォ","FO"},  {"ヴォ","VO"},  
              {"ウィ","WI"},  {"ウェ","WE"},  {"ウォ","WO"},
              {"ヴャ","VYA"}, {"ヴョ","VYO"},
              {"クヮ","KWA"}, {"グヮ","GWA"}, {"クィ","KWI"}, {"グィ","GWI"},
              {"グァ","GWA"}, 
              {"クェ","KWE"}, {"グェ","GWE"}, {"クォ","KWO"}, {"グォ","GWO"}, 
               {"ヂョ","JO"},  {"ヴァ","VA"},   {"ヴィ","VI"},  {"スィ","SI"}
            };
         }
         * */

		string ConvertKanaToLatin(string str)
		{
			string[] Kana = new string[] {"イェ","きゃ", "しゃ", "ちゃ", "にゃ", "ひゃ", "みゃ", "りゃ", "ぎゃ", "びゃ", "ぴゃ", "きゅ", "しゅ", "ちゅ", "にゅ", "ひゅ", "みゅ", "りゅ", "ぎゅ", "びゅ", "ぴゅ", "きょ", "しょ", "ちょ", "にょ", "ひょ", "みょ", "りょ", "ぎょ", "びょ", "ぴょ", "じゃ", "ぢゃ", "じゅ", "ぢゅ", "じょ", "ぢょ", "じ", "ぢ", "ず", "づ", "ん", "し", "ち", "つ", "か", "さ", "た", "な", "は", "ま", "や", "ら", "わ", "が", "ざ", "だ", "ば", "ぱ", "き", "に", "ひ", "み", "り", "ゐ", "ぎ", "び", "ぴ", "く", "す", "ぬ", "ふ", "む", "ゆ", "る", "ぐ", "ぶ", "ぷ", "け", "せ", "て", "ね", "へ", "め", "れ", "ゑ", "げ", "ぜ", "で", "べ", "ぺ", "こ", "そ", "と", "の", "ほ", "も", "よ", "ろ", "を", "ご", "ぞ", "ど", "ぼ", "ぽ", "あ", "い", "う", "え", "お",
			"キャ", "シャ", "チャ", "ニャ", "ヒャ", "ミャ", "リャ", "ギャ", "ビャ", "ピャ", "ヴャ", "キュ", "シュ", "チュ", "ニュ", "ヒュ", "ミュ", "リュ", "ギュ", "ビュ", "ピュ", "ヴュ", "テュ", "デュ", "フュ", "キョ", "ショ", "チョ", "ニョ", "ヒョ", "ミョ", "リョ", "ギョ", "ビョ", "ピョ", "ヴョ", "ツァ", "クヮ", "グヮ", "ツィ", "クィ", "グィ", "シェ", "チェ", "ツェ", "クェ", "グェ", "ツォ", "クォ", "グォ", "ジャ", "ヂャ", "ジュ", "ヂュ", "ジョ", "ヂョ", "ヴァ", "ファ", "ヴィ", "スィ", "ズィ", "ティ", "ディ", "フィ", "ウィ", "ヴェ", "ジェ", "フェ", "ウェ", "ヴォ", "フォ", "ウォ", "クヮ", "グァ", "トゥ", "ドゥ", "シ", "チ", "ツ", "ジ", "ヂ", "ズ", "ヅ", "ン", "カ", "サ", "タ", "ナ", "ハ", "マ", "ヤ", "ラ", "ワ", "ガ", "ザ", "ダ", "バ", "パ", "キ", "ニ", "ヒ", "ミ", "リ", "ヰ", "ギ", "ビ", "ピ", "ク", "ス", "ヌ", "フ", "ム", "ユ", "ル", "グ", "ブ", "プ", "ヴ", "コ", "ソ", "ト", "ノ", "ホ", "モ", "ヨ", "ロ", "ヲ", "ゴ", "ゾ", "ド", "ボ", "ポ", "ケ", "セ", "テ", "ネ", "ヘ", "メ", "レ", "ヱ", "ゲ", "ゼ", "デ", "ベ", "ペ", "ア", "イ", "ウ", "エ", "オ",
            };
            string[] LatinKana = new string[] {"YE","kya", "sha", "cha", "nya", "hya", "mya", "rya", "gya", "bya", "pya", "kyu", "shu", "chu", "nyu", "hyu", "myu", "ryu", "gyu", "byu", "pyu", "kyo", "sho", "cho", "nyo", "hyo", "myo", "ryo", "gyo", "byo", "pyo","ja", "ja", "ju", "ju", "jo", "jo", "ji", "ji", "zu", "zu", "n", "shi", "chi", "tsu", "ka", "sa", "ta", "na", "ha", "ma", "ya", "ra", "wa", "ga", "za", "da", "ba", "pa", "ki", "ni", "hi", "mi", "ri", "wi", "gi", "bi", "pi", "ku", "su", "nu", "fu", "mu", "yu", "ru", "gu", "bu", "pu", "ke", "se", "te", "ne", "he", "me", "re", "we", "ge", "ze", "de", "be", "pe", "ko", "so", "to", "no", "ho", "mo", "yo", "ro", "wo", "go", "zo", "do", "bo", "po", "a", "i", "u", "e", "o",
			"KYA", "SHA", "CHA", "NYA", "HYA", "MYA", "RYA", "GYA", "BYA", "PYA", "VYA", "KYU", "SHU", "CHU", "NYU", "HYU", "MYU", "RYU", "GYU", "BYU", "PYU", "VYU", "TYU", "DYU", "FYU", "KYO", "SHO", "CHO", "NYO", "HYO", "MYO", "RYO", "GYO", "BYO", "PYO", "VYO", "TSA", "KWA", "GWA", "TSI", "KWI", "GWI", "SHE", "CHE", "TSE", "KWE", "GWE", "TSO", "KWO", "GWO", "JA", "JA", "JU", "JU", "JO", "JO", "VA", "FA", "VI", "SI", "ZI", "TI", "DI", "FI", "WI", "VE", "JE", "FE", "WE", "VO", "FO", "WO", "KWA", "GWA", "TU", "DU", "SHI", "CHI", "TSU", "JI", "JI", "ZU", "ZU", "N", "KA", "SA", "TA", "NA", "HA", "MA", "YA", "RA", "WA", "GA", "ZA", "DA", "BA", "PA", "KI", "NI", "HI", "MI", "RI", "WI", "GI", "BI", "PI", "KU", "SU", "NU", "FU", "MU", "YU", "RU", "GU", "BU", "PU", "VU", "KO", "SO", "TO", "NO", "HO", "MO", "YO", "RO", "WO", "GO", "ZO", "DO", "BO", "PO", "KE", "SE", "TE", "NE", "HE", "ME", "RE", "WE", "GE", "ZE", "DE", "BE", "PE", "A", "I", "U", "E", "O",
            };
			
			int kana_len = Kana.Length;
			int len = str.Length;
			int pos = 0;
			string res = "";
			int i;
			int pos_in_kana;
			char tmp;
			bool doubled = false;
			while (pos < len)
			{
				pos_in_kana = -1;
				for (i = 0; i < kana_len; i++)
				{
					if (str.IndexOf(Kana[i], pos) == pos)
					{
						pos_in_kana = i;
						break;
					}
				}
				if (pos_in_kana > -1)
				{
					if (!doubled)
					{
						res = res + LatinKana[pos_in_kana];
					}
					else
					{
						res = res + LatinKana[pos_in_kana][0] + LatinKana[pos_in_kana];
					}
					pos = pos + Kana[pos_in_kana].Length;
					doubled = false;
				}
				else
				{
					if ((str[pos] == 'っ') || (str[pos] == 'ッ'))
					{
						doubled = true;
					}
					else
					if ((str[pos] == 'ー') && (res.Length != 0))
					{
						tmp = res[res.Length - 1];
						if (isVowel(tmp) && Char.IsUpper(tmp))
						{
							res = res + tmp;
						}
						else
						{
							res = res + "-";
						}
					}
					else
					{
						res = res + ConvertSmallKanaCharToLatinStr(str[pos]);
                        //Debugger.Log(0, "", str + "converted to " + res + Environment.NewLine);
						doubled = false;
					}
					pos++;
				}
			}
			Kana = null;
			LatinKana = null;
            return res.ToLower();
		}
		bool isVowel(char chr)
		{
			const string vowels = "aiueoAIUEO";
            return vowels.Contains(chr);
		}
		string ConvertSmallKanaCharToLatinStr(char chr)
		{
			char[] SKana = new char[] {'ぁ', 'ぃ', 'ぅ', 'ぇ', 'ぉ', 'っ', 'ゃ', 'ょ', 'ゎ', 'ァ', 'ィ', 'ゥ', 'ェ', 'ォ', 'ッ', 'ャ', 'ュ', 'ョ', 'ヮ', 'ヵ', 'ヶ'};
			string[] SLatinKana = new string[] {"(a)", "(i)", "(u)", "(e)", "(o)", "(tsu)", "(ya)", "(yo)", "(wa)", "(A)", "(I)", "(U)", "(E)", "(O)", "(TSU)", "(YA)", "(YU)", "(YO)", "(WA)", "(KA)", "(KE)"};
			
			int skana_len = SKana.Length;
			string res = chr.ToString();
			int i;
			for (i = 0; i < skana_len; i++)
			{
				if (SKana[i] == chr)
				{
                    res = SLatinKana[i];
					break;
				}
			}
			SKana = null;
			SLatinKana = null;
			return res;
		}
    }
}
