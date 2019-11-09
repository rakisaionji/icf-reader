using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace ICFReader
{
    public partial class FrmMain : Form
    {
        private string lastError;
        private string currentAppName;
        private List<string> dataList;

        public FrmMain()
        {
            InitializeComponent();
        }

        private bool ReadStorageInfo(string path)
        {
            var encoding = Encoding.UTF8;
            short vmajor; byte vminor, vbuild; // Version
            short yy, mm, dd, hh, mi, ss; // Date
            var datetime = DateTime.MinValue;
            var appdate = DateTime.MinValue;
            var sysver = new Version();
            var appver = new Version();
            var vzero = new Version(0, 0, 0);

            if (!File.Exists(path))
            {
                lastError = "File Not Found";
                return false;
            }

            var decrypted = Sensitive.DecryptICF(path);
            if (decrypted == null) { lastError = "Invalid ICF file"; return false; }

            var data = new byte[decrypted.Length - 4];
            Array.Copy(decrypted, 4, data, 0, data.Length);
            var dtc1 = Checksum.CRC32(data);

            using (var rd = new BinaryReader(new MemoryStream(decrypted)))
            {
                // Check Main CRC32
                var crc1 = rd.ReadUInt32();
                if (crc1 != dtc1) { lastError = "Main Data Error"; return false; }
                // Check Size
                var size = rd.ReadUInt32();
                if (size != decrypted.Length) { lastError = "Data Size Error"; return false; }
                // Check padding
                var padding = rd.ReadUInt64();
                if (padding != 0) { lastError = "Data Padding Error"; return false; }

                // Entry count
                var count = (int)rd.ReadUInt64();
                var expsz = 0x40 * (count + 1);
                if (expsz != decrypted.Length) { lastError = "Data Info Error"; return false; }
                // Read App Id
                var appid = encoding.GetString(rd.ReadBytes(4));
                // Read Platform Id
                var platid = encoding.GetString(rd.ReadBytes(3));
                // Read Platform Generation
                var platgen = rd.ReadByte();

                // Check Sub CRC32
                var crc2 = rd.ReadUInt32();
                uint dtc2 = 0;
                for (int i = 1; i <= count; i++)
                {
                    data = new byte[0x40];
                    Array.Copy(decrypted, 0x40 * i, data, 0, data.Length);
                    if (data[0] == 2 && data[1] == 1) dtc2 ^= Checksum.CRC32(data);
                }
                if (crc2 != dtc2) { lastError = "Sub Data Error"; return false; }
                // Check padding
                for (int i = 0; i < 7; i++)
                {
                    padding = rd.ReadUInt32();
                    if (padding != 0) { lastError = "Data Padding Error"; return false; }
                }

                // Begin Parse Data
                dataList = new List<string>();
                for (int c = 0; c < count; c++)
                {
                    // Begin Entry
                    data = rd.ReadBytes(4);
                    // Part Start
                    var enabled = (data[0] == 2 && data[1] == 1);
                    // Part Type
                    // 00 00 : System, 01 00 : Main , 01 01 : Patch , 02 00 : Option
                    var type = rd.ReadUInt32();
                    // Check padding
                    for (int i = 0; i < 3; i++)
                    {
                        padding = rd.ReadUInt64();
                        if (padding != 0) { lastError = "Data Padding Error"; return false; }
                    }
                    switch (type)
                    {
                        case 0x0000: // SYSTEM
                            vbuild = rd.ReadByte();
                            vminor = rd.ReadByte();
                            vmajor = rd.ReadInt16();
                            sysver = new Version(vmajor, vminor, vbuild);
                            yy = rd.ReadInt16();
                            mm = rd.ReadByte();
                            dd = rd.ReadByte();
                            hh = rd.ReadByte();
                            mi = rd.ReadByte();
                            ss = rd.ReadByte();
                            rd.ReadByte(); // ms, not use
                            datetime = new DateTime(yy, mm, dd, hh, mi, ss);
                            // Check SystemVersion Requirement
                            vbuild = rd.ReadByte();
                            vminor = rd.ReadByte();
                            vmajor = rd.ReadInt16();
                            var ver = new Version(vmajor, vminor, vbuild);
                            if (!ver.Equals(sysver)) { lastError = "System Version Error"; return false; }
                            // Check Padding
                            for (int i = 0; i < 2; i++)
                            {
                                padding = rd.ReadUInt64();
                                if (padding != 0) { lastError = "Data Padding Error"; return false; }
                            }
                            dataList.Add(String.Format("{0}_{1:D4}.{2:D2}.{3:D2}_{4:yyyyMMddHHmmss}_0.pack", platid, sysver.Major, sysver.Minor, sysver.Build, datetime));
                            break;
                        case 0x0001: // APP
                            vbuild = rd.ReadByte();
                            vminor = rd.ReadByte();
                            vmajor = rd.ReadInt16();
                            appver = new Version(vmajor, vminor, vbuild);
                            yy = rd.ReadInt16();
                            mm = rd.ReadByte();
                            dd = rd.ReadByte();
                            hh = rd.ReadByte();
                            mi = rd.ReadByte();
                            ss = rd.ReadByte();
                            rd.ReadByte(); // ms, not use
                            appdate = new DateTime(yy, mm, dd, hh, mi, ss);
                            // Check SystemVersion Requirement
                            vbuild = rd.ReadByte();
                            vminor = rd.ReadByte();
                            vmajor = rd.ReadInt16();
                            ver = new Version(vmajor, vminor, vbuild);
                            if (!ver.Equals(sysver)) { lastError = "System Version Error"; return false; }
                            // Check Padding
                            for (int i = 0; i < 2; i++)
                            {
                                padding = rd.ReadUInt64();
                                if (padding != 0) { lastError = "Data Padding Error"; return false; }
                            }
                            dataList.Add(String.Format("{0}_{1:D}.{2:D2}.{3:D2}_{4:yyyyMMddHHmmss}_0.app", appid, appver.Major, appver.Minor, appver.Build, appdate));
                            break;
                        case 0x0101: // PATCH
                            var vers = new List<Version>();
                            var dats = new List<DateTime>();
                            for (int i = 0; i < 2; i++)
                            {
                                vbuild = rd.ReadByte();
                                vminor = rd.ReadByte();
                                vmajor = rd.ReadInt16();
                                vers.Add(new Version(vmajor, vminor, vbuild));
                                yy = rd.ReadInt16();
                                mm = rd.ReadByte();
                                dd = rd.ReadByte();
                                hh = rd.ReadByte();
                                mi = rd.ReadByte();
                                ss = rd.ReadByte();
                                rd.ReadByte(); // ms, not use
                                dats.Add(new DateTime(yy, mm, dd, hh, mi, ss));
                                // Check SystemVersion Requirement
                                vbuild = rd.ReadByte();
                                vminor = rd.ReadByte();
                                vmajor = rd.ReadInt16();
                                ver = new Version(vmajor, vminor, vbuild);
                                if (!ver.Equals(sysver) && !ver.Equals(vzero)) { lastError = "System Version Error"; return false; }
                            }
                            // Check Patch Info
                            if (!vers[1].Equals(appver)) { lastError = "Application Version Error"; return false; }
                            if (!dats[1].Equals(appdate)) { lastError = "Application Timestamp Error"; return false; }
                            dataList.Add(String.Format("{0}_{1:D}.{2:D2}.{3:D2}_{4:yyyyMMddHHmmss}_1_{5:D}.{6:D2}.{7:D2}.app", appid, vers[0].Major, vers[0].Minor, vers[0].Build, dats[0], vers[1].Major, vers[1].Minor, vers[1].Build));
                            break;
                        case 0x0002: // OPTION
                            var optid = encoding.GetString(rd.ReadBytes(4));
                            yy = rd.ReadInt16();
                            mm = rd.ReadByte();
                            dd = rd.ReadByte();
                            hh = rd.ReadByte();
                            mi = rd.ReadByte();
                            ss = rd.ReadByte();
                            rd.ReadByte(); // ms, not use
                            datetime = new DateTime(yy, mm, dd, hh, mi, ss);
                            // Check Padding
                            for (int i = 0; i < 5; i++)
                            {
                                padding = rd.ReadUInt32();
                                if (padding != 0) { lastError = "Data Padding Error"; return false; }
                            }
                            dataList.Add(String.Format("{0}_{1}_{2:yyyyMMddHHmmss}_0.opt", appid, optid, datetime));
                            break;
                        default:
                            rd.ReadBytes(0x20);
                            break;
                    }
                }
                /*
                    // Check signature
                    rd.BaseStream.Seek(0x8, SeekOrigin.Begin);
                    var sign = encoding.GetString(rd.ReadBytes(4));
                    if (!sign.Equals("BTID"))
                    {
                        MessageBox.Show("Invalid Boot ID file");
                        return false;
                    }
                    rd.ReadByte();
                    currentData = new RingBootId();
                    var count = rd.ReadByte();
                    if (count > 2) count = 2;
                    currentData.AppInfo = new RingAppInfo[count];
                    // Read basic info
                    rd.BaseStream.Seek(0x10, SeekOrigin.Begin);
                    currentData.AppId = encoding.GetString(rd.ReadBytes(4));
                    for (int i = 0; i < count; i++)
                    {
                        var appif = new RingAppInfo();
                        yy = rd.ReadInt16();
                        mm = rd.ReadByte();
                        dd = rd.ReadByte();
                        hh = rd.ReadByte();
                        mi = rd.ReadByte();
                        ss = rd.ReadByte();
                        rd.ReadByte();
                        vminor = rd.ReadInt16();
                        vmajor = rd.ReadInt16();
                        appif.Date = new DateTime(yy, mm, dd, hh, mi, ss);
                        appif.Version = new Version(vmajor, vminor);
                        appif.AppId = currentData.AppId;
                        currentData.AppInfo[count - i - 1] = appif;
                        rd.ReadBytes(20); // Skip 20 bytes of unknown shit
                    }
                    // Read app name
                    byte buf;
                    var arr = new List<byte>();
                    rd.BaseStream.Seek(0x60, SeekOrigin.Begin);
                    do
                    {
                        buf = rd.ReadByte();
                        if (buf != 0) arr.Add(buf);
                        else break;
                    }
                    while (true);
                    currentData.AppName = encoding.GetString(arr.ToArray());
                    // That's all so far
                    */
                dataList.Sort();
                currentAppName = appid;
                return true;
            }
        }

        private void LoadStorageInfo()
        {
            var path = txtFileName.Text.Trim();
            var read = ReadStorageInfo(path);

            txtOutput.Clear();
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("STORAGE INFORMATION");
            sb.AppendLine();
            if (!read)
            {
                sb.AppendLine("--- ERROR ---");
                sb.AppendLine(lastError);
                txtOutput.Text = sb.ToString();
                return;
            }
            sb.AppendFormat("[{0}]", currentAppName);
            sb.AppendLine();
            sb.AppendLine();
            foreach (var data in dataList)
            {
                sb.AppendLine(data);
            }
            sb.AppendLine();
            sb.AppendLine("- END -");
            sb.AppendLine();
            txtOutput.Text = sb.ToString();
        }

        private void FrmMain_Load(object sender, EventArgs e)
        {
            txtFileName.Clear();
            txtOutput.Clear();
        }

        private void BtnOpen_Click(object sender, EventArgs e)
        {
            var od = new OpenFileDialog();
            od.Title = "Open File";
            od.Filter = "ICF Files (*.icf)|ICF?;*.icf|All Files (*.*)|*.*";
            od.Multiselect = false;
            od.CheckFileExists = true;
            if (od.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var path = od.FileName;
                    txtFileName.Text = path;
                    LoadStorageInfo();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error: {ex.Message}");
                }
            }
        }
    }
}
