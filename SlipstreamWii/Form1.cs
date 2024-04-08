using System;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;

namespace SlipstreamWii
{
    public partial class Form1 : Form
    {
        public bool loading;
        public string mkwFilePath;
        public bool complexSampling;
        public Dictionary<string, MKW_Character> characters = new Dictionary<string, MKW_Character>();
        public Dictionary<string, MKW_Vehicle> vehicles = new Dictionary<string, MKW_Vehicle>();
        public List<string> vehicleTypes = new List<string>();
        public Form1()
        {
            InitializeComponent();
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            loading = true;
            progressLabel.Text = "";
            mkwFilePath = Properties.Settings.Default.mkwPath;
            targetLanguageBox.Text = Properties.Settings.Default.uiLanguage;
            UpdateSamplingType();
            ParseMKWParameters();
            targetCharBox.Items.AddRange(characters.Keys.ToArray());
            if (targetCharBox.Items.Count > 0) targetCharBox.SelectedIndex = 0;
            RefreshVehicleGenerationList();
            loading = false;
        }
        private void openMKWFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Please select MKW's 'files' folder");
            FolderBrowserDialog open = new FolderBrowserDialog();
            if (open.ShowDialog() == DialogResult.OK)
            {
                mkwFilePath = open.SelectedPath;
                Properties.Settings.Default.mkwPath = mkwFilePath;
                Properties.Settings.Default.Save();
            }
        }
        private void extractFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog open = new OpenFileDialog();
            open.Title = "Extract a (.szs) or (.brres) file";
            open.Filter = "Wii File (*.szs;*.brres)|*.szs;*.brres|All files (*.*)|*.*";
            if (open.ShowDialog() == DialogResult.OK)
            {
                string predictedPath = open.FileName;
                if (predictedPath.EndsWith(".szs")) predictedPath = predictedPath.Substring(0, predictedPath.Length - 4);
                predictedPath = predictedPath + ".d";

                if (Directory.Exists(predictedPath)) Directory.Delete(predictedPath, true);
                Process extractUI = new Process();
                extractUI.StartInfo = extractFile(open.FileName, "");
                extractUI.Start();
                extractUI.WaitForExit();
                extractUI.Dispose();
            }
        }
        private void buildFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog open = new FolderBrowserDialog();
            if (open.ShowDialog() == DialogResult.OK)
            {
                string predictedPath = open.SelectedPath.Substring(0, open.SelectedPath.Length - 2);
                string modifier = "";
                if (predictedPath.EndsWith(".brres")) modifier = "--brres --no-compress ";
                else predictedPath = predictedPath + ".szs";

                if (File.Exists(predictedPath)) File.Delete(predictedPath);
                Process extractUI = new Process();
                extractUI.StartInfo = createFile(open.SelectedPath, modifier);
                extractUI.Start();
                extractUI.WaitForExit();
                extractUI.Dispose();
            }
        }
        private void decodeBmgToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog open = new OpenFileDialog();
            open.Title = "Extract a (.bmg) file";
            open.Filter = "MKW Message File (*.bmg;)|*.bmg;|All files (*.*)|*.*";
            if (open.ShowDialog() == DialogResult.OK)
            {
                Process extractUI = new Process();
                extractUI.StartInfo = extractBMG(open.FileName, "");
                extractUI.Start();
                extractUI.WaitForExit();
                extractUI.Dispose();
            }
        }
        private void encodeBmgToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog open = new OpenFileDialog();
            open.Title = "Create a (.bmg) file";
            open.Filter = "MKW Message File (*.txt;)|*.txt;|All files (*.*)|*.*";
            if (open.ShowDialog() == DialogResult.OK)
            {
                Process extractUI = new Process();
                extractUI.StartInfo = createBMG(open.FileName, "");
                extractUI.Start();
                extractUI.WaitForExit();
                extractUI.Dispose();
            }
        }
        private void targetLanguageBox_TextChanged(object sender, EventArgs e)
        {
            if (loading) return;
            Properties.Settings.Default.uiLanguage = targetLanguageBox.Text;
            Properties.Settings.Default.Save();
        }
        private void changeSamplingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            complexSampling = !complexSampling;
            Properties.Settings.Default.complexSampling = complexSampling;
            Properties.Settings.Default.Save();
            UpdateSamplingType();
        }
        private void createSampleBtn_Click(object sender, EventArgs e)
        {
            // Check if files folder is defined, prompt user if not
            if (!Directory.Exists(mkwFilePath) || !mkwFilePath.EndsWith("files"))
            {
                openMKWFilesToolStripMenuItem_Click(sender, e);
                return;
            }

            // Define a Target Driver
            if (targetCharBox.SelectedIndex == -1)
            {
                MessageBox.Show("Please select a valid character to replace");
                return;
            }
            MKW_Character target = characters[targetCharBox.Text];
            bool complexCheck = false;
            if (!complexSampling && target.isComplex)
            {
                DialogResult check = MessageBox.Show($"{targetCharBox.Text} is a 'complex' character, switch sampling type?" + Environment.NewLine + 
                    "This allows you to have different outfits for Karts and Bikes.", "", MessageBoxButtons.YesNo);
                if (check == DialogResult.Yes)
                {
                    changeSamplingToolStripMenuItem_Click(sender, e);
                    complexCheck = true;
                }
            }

            // Save a sample model from target
            SaveFileDialog save = new SaveFileDialog();
            save.Title = "Save a szs model to sample files from.";
            save.FileName = "NewSample.sample.szs";
            save.Filter = "MKW Sample Model File (*.sample.szs)|*.sample.szs|All files (*.*)|*.*";
            if (save.ShowDialog() == DialogResult.OK)
            {
                SaveSample(save.FileName, target, "Kart");
                if (complexCheck) changeSamplingToolStripMenuItem_Click(sender, e);
            }
        }
        private void createFilesBtn_Click(object sender, EventArgs e)
        {
            // Check if files folder is defined, prompt user if not
            if (!Directory.Exists(mkwFilePath) || !mkwFilePath.EndsWith("files"))
            {
                openMKWFilesToolStripMenuItem_Click(sender, e);
                return;
            }

            // Define a Target Driver
            if (targetCharBox.SelectedIndex == -1)
            {
                MessageBox.Show("Please select a valid character to replace");
                return;
            }

            // Output edited files from a sample model.
            OpenFileDialog open = new OpenFileDialog();
            open.Title = "Save a szs model to sample files from.";
            open.Multiselect = true;
            open.Filter = "MKW Sample Model File (*.sample.szs)|*.sample.szs|All files (*.*)|*.*";
            if (open.ShowDialog() == DialogResult.OK)
            {
                CreateFilesFromSample(open.FileNames);
            }
        }
        private void SaveSample(string path, MKW_Character target, string mainModelType)
        {
            // Extract a model to a temporary folder to pull files from.
            string folderPath = Path.GetDirectoryName(path) + "\\" + Path.GetFileNameWithoutExtension(path) + ".d";
            string tempPath = AppDomain.CurrentDomain.BaseDirectory + "\\Temp";
            if (Directory.Exists(folderPath)) Directory.Delete(folderPath, true);
            if (Directory.Exists(tempPath)) Directory.Delete(tempPath, true);
            Directory.CreateDirectory(folderPath);
            Directory.CreateDirectory(tempPath);

            // Find the first available kart models for the target's size type.
            Dictionary<string, string> firstVehicleTypeByPath = new Dictionary<string, string>();
            foreach (string v_type in vehicleTypes)
            {
                firstVehicleTypeByPath.Add(AvailableVehiclePaths(target.size, target.abbrev, v_type)[0], v_type);
            }

            // Create Folders within sample folder
            Directory.CreateDirectory(folderPath + "\\vehicles");
            if (!complexSampling) Directory.CreateDirectory(folderPath + "\\driver.brres.d");

            // Get list of compatible vehicles to check
            List<string> keysToCheck = new List<string>();
            foreach (string key in vehicles.Keys)
            {
                if (vehicles[key].size == target.size) keysToCheck.Add(key);
            }
            globalProgress.Value = 0;
            globalProgress.Maximum = keysToCheck.Count + 8;

            // Extract allkart models to be rebuilt later
            progressLabel.Text = "Fetching target's allkart models";
            Process extractAllKart = new Process();
            if (File.Exists(mkwFilePath + $"\\Scene\\Model\\Kart\\{target.abbrev}-allkart.szs"))
            {
                File.Copy(mkwFilePath + $"\\Scene\\Model\\Kart\\{target.abbrev}-allkart.szs", tempPath + "\\allkart.szs");
                extractAllKart = new Process();
                extractAllKart.StartInfo = extractFile(tempPath + "\\allkart.szs", "");
                extractAllKart.Start();
                extractAllKart.WaitForExit();
                extractAllKart.Dispose();
                File.Delete(tempPath + "\\allkart.szs");
            }
            if (File.Exists(mkwFilePath + $"\\Scene\\Model\\Kart\\{target.abbrev}-allkart_BT.szs"))
            {
                File.Copy(mkwFilePath + $"\\Scene\\Model\\Kart\\{target.abbrev}-allkart_BT.szs", tempPath + "\\allkart_BT.szs");
                extractAllKart = new Process();
                extractAllKart.StartInfo = extractFile(tempPath + "\\allkart_BT.szs", "");
                extractAllKart.Start();
                extractAllKart.WaitForExit();
                extractAllKart.Dispose();
                File.Delete(tempPath + "\\allkart_BT.szs");
            }
            globalProgress.Value++;

            for (int i = 0; i < keysToCheck.Count; i++)
            {
                string a = vehicles[keysToCheck[i]].abbrev;
                if (File.Exists(tempPath + "\\vehicle.szs")) File.Delete(tempPath + "\\vehicle.szs");
                if (Directory.Exists(tempPath + "\\vehicle.d")) Directory.Delete(tempPath + "\\vehicle.d", true);

                progressLabel.Text = $"Sampling files from {keysToCheck[i]}...";

                // Copy Vehicle files from mkw Race folder to the temporary folder
                string vehiclePath = mkwFilePath + $"\\Race\\Kart\\{a}-{target.abbrev}.szs";
                if (File.Exists(vehiclePath)) File.Copy(vehiclePath, tempPath + "\\vehicle.szs");
                else
                {
                    Debug.WriteLine("Failed to find vehicle: " + vehiclePath);
                    continue;
                }

                // Extract Vehicle from copied file to a folder
                Process extractKart = new Process();
                extractKart.StartInfo = extractFile(tempPath + "\\vehicle.szs", "");
                extractKart.Start();
                extractKart.WaitForExit();
                extractKart.Dispose();

                // Save the path of the extracted kart folder
                string k = tempPath + "\\vehicle.d";

                // Move vehicle's model to sample folder
                string vehicleModelPath = k + "\\kart_model.brres";
                if (File.Exists(vehicleModelPath))
                {
                    string mod = "";
                    if (a.EndsWith("blue") || a.EndsWith("red")) mod = "_BT";

                    if (File.Exists(tempPath + $"\\allkart{mod}.d\\{a}.brres"))
                    {
                        // If a menu version of the model exists in allkart, extract it's model AND this vehicle, then combine them.
                        extractKart = new Process();
                        extractKart.StartInfo = extractFile(vehicleModelPath, "");
                        extractKart.Start();
                        extractKart.WaitForExit();
                        extractKart.Dispose();
                        File.Delete(vehicleModelPath);

                        string allkartPath = tempPath + $"\\allkart{mod}.d\\{a}.brres";
                        extractKart = new Process();
                        extractKart.StartInfo = extractFile(allkartPath, "");
                        extractKart.Start();
                        extractKart.WaitForExit();
                        extractKart.Dispose();
                        File.Delete(allkartPath);
                        Directory.Move(allkartPath + ".d\\3DModels(NW4R)", vehicleModelPath + ".d\\Menu_3DModels(NW4R)");
                        if (vehicles[keysToCheck[i]].usesUniqueMenuTextures)
                        {
                            Directory.Move(allkartPath + ".d\\Textures(NW4R)", vehicleModelPath + ".d\\Menu_Textures(NW4R)");
                        }
                        Directory.Delete(allkartPath + ".d", true);

                        Process buildKart = new Process();
                        buildKart.StartInfo = createFile(vehicleModelPath + ".d", "");
                        buildKart.Start();
                        buildKart.WaitForExit();
                        buildKart.Dispose();
                        Directory.Delete(vehicleModelPath + ".d", true);
                    }
                    File.Copy(vehicleModelPath, folderPath + $"\\vehicles\\{keysToCheck[i].Replace(" ", "_")}.brres");
                }
                else Debug.WriteLine("Failed to find vehicle model: " + vehicleModelPath);

                string driverModelPath = k + "\\driver_model.brres";
                if (File.Exists(driverModelPath))
                {
                    if (firstVehicleTypeByPath.ContainsKey(vehiclePath))
                    {
                        string type = firstVehicleTypeByPath[vehiclePath].ToLower().Replace(" ", "_");
                        if (complexSampling)
                        {
                            // Simply move and rename brres
                            File.Copy(driverModelPath, folderPath + $"\\driving_{type}.brres");
                        }
                        else
                        {
                            Directory.CreateDirectory(folderPath + $"\\driving_{type}.brres.d");
                            Process extractDriverBrres = new Process();
                            extractDriverBrres.StartInfo = extractFile(driverModelPath, "");
                            extractDriverBrres.Start();
                            extractDriverBrres.WaitForExit();
                            extractDriverBrres.Dispose();

                            // If this is the main model type, copy the brres's Model and Textures
                            if (type.ToLower() == mainModelType.ToLower())
                            {
                                if (File.Exists(driverModelPath + ".d\\3DModels(NW4R)\\model"))
                                {
                                    Directory.CreateDirectory(folderPath + $"\\driver.brres.d\\3DModels(NW4R)");
                                    File.Copy(driverModelPath + ".d\\3DModels(NW4R)\\model", folderPath + $"\\driver.brres.d\\3DModels(NW4R)\\model", true);
                                }
                                if (Directory.Exists(driverModelPath + ".d\\Textures(NW4R)"))
                                    Directory.Move(driverModelPath + ".d\\Textures(NW4R)", folderPath + $"\\driver.brres.d\\Textures(NW4R)");
                            }

                            // Move LOD model into its respective folder
                            if (File.Exists(driverModelPath + ".d\\3DModels(NW4R)\\model_lod"))
                            {
                                if (!Directory.Exists(folderPath + $"\\driver.brres.d\\LOD_{type}"))
                                    Directory.CreateDirectory(folderPath + $"\\driver.brres.d\\LOD_{type}");

                                //Directory.CreateDirectory(folderPath + $"\\driver.brres.d\\LOD\\{type}");
                                File.Move(driverModelPath + ".d\\3DModels(NW4R)\\model_lod", folderPath + $"\\driver.brres.d\\LOD_{type}\\model_lod");
                            }

                            // Move anims into their respective brres folders
                            foreach (string dir in Directory.GetDirectories(driverModelPath + ".d"))
                            {
                                string matchingFolder = Path.GetFileName(dir);
                                if (matchingFolder != "3DModels(NW4R)" && matchingFolder != "Textures(NW4R)")
                                {
                                    Directory.Move(dir, folderPath + $"\\driving_{type}.brres.d\\{matchingFolder}");
                                }
                            }
                        }
                    }
                }
                else Debug.WriteLine("Failed to find driver model: " + driverModelPath);
                globalProgress.Value++;
            }

            // Get Main Menu Animations
            progressLabel.Text = "Fetching Main Menu Animations...";
            string selectCharAnimsPath = mkwFilePath + "\\Scene\\Model\\Driver.szs";
            string selectKartAnimsPath = mkwFilePath + $"\\Scene\\Model\\Kart\\{target.abbrev}-allkart.szs";
            if (File.Exists(selectCharAnimsPath))
            {
                File.Copy(selectCharAnimsPath, tempPath + "\\SelectChar.szs");
                Process extractMenu = new Process();
                extractMenu.StartInfo = extractFile(tempPath + "\\SelectChar.szs", "");
                extractMenu.Start();
                extractMenu.WaitForExit();
                extractMenu.Dispose();

                string[] modifiers = new string[2] { "", "_menu" };
                foreach (string mod in modifiers)
                {
                    if (File.Exists(tempPath + $"\\SelectChar.d\\{target.abbrev}{mod}.brres"))
                    {
                        if (complexSampling)
                        {
                            File.Copy(tempPath + $"\\SelectChar.d\\{target.abbrev}{mod}.brres", folderPath + "\\select_char.brres", true);
                        }
                        else
                        {
                            Directory.CreateDirectory(folderPath + "\\select_char.brres.d");
                            extractMenu = new Process();
                            extractMenu.StartInfo = extractFile(tempPath + $"\\SelectChar.d\\{target.abbrev}{mod}.brres", "");
                            extractMenu.Start();
                            extractMenu.WaitForExit();
                            extractMenu.Dispose();

                            foreach (string dir in Directory.GetDirectories(tempPath + $"\\SelectChar.d\\{target.abbrev}{mod}.brres.d"))
                            {
                                string matchingFolder = Path.GetFileName(dir);
                                if (matchingFolder != "3DModels(NW4R)" && matchingFolder != "Textures(NW4R)")
                                {
                                    Directory.Move(dir, folderPath + $"\\select_char.brres.d\\{matchingFolder}");
                                }
                            }
                        }
                    }
                }
            }
            else Debug.WriteLine("Failed to find driver.szs at: " + selectCharAnimsPath);

            if (File.Exists(selectKartAnimsPath))
            {
                File.Copy(selectKartAnimsPath, tempPath + "\\SelectKart.szs", true);
                Process extractMenu = new Process();
                extractMenu.StartInfo = extractFile(tempPath + "\\SelectKart.szs", "");
                extractMenu.Start();
                extractMenu.WaitForExit();
                extractMenu.Dispose();
                if (File.Exists(tempPath + "\\SelectKart.d\\driver_anim.brres"))
                {
                    File.Copy(tempPath + "\\SelectKart.d\\driver_anim.brres", folderPath + "\\select_kart.brres", true);
                }
                else Debug.WriteLine("Failed to find allkart's driver animations at: " + tempPath + "\\SelectKart.d\\driver_anim.brres");
            }
            else Debug.WriteLine("Failed to find allkart.szs at: " + selectKartAnimsPath);
            globalProgress.Value++;

            // Get Award Ceremony Model / Animations
            progressLabel.Text = "Fetching Award Ceremony Model...";
            string awardAnimsPath = mkwFilePath + "\\Demo\\Award.szs";
            if (File.Exists(selectCharAnimsPath))
            {
                File.Copy(awardAnimsPath, tempPath + "\\Award.szs");
                Process extractAwardsSZS = new Process();
                extractAwardsSZS.StartInfo = extractFile(tempPath + "\\Award.szs", "");
                extractAwardsSZS.Start();
                extractAwardsSZS.WaitForExit();
                extractAwardsSZS.Dispose();

                string[] complexAwardSampling = new string[2] { "", "3" };
                foreach (string mod in complexAwardSampling)
                {
                    if (File.Exists(tempPath + $"\\Award.d\\{target.abbrev}{mod}.brres"))
                    {
                        Directory.CreateDirectory(folderPath + $"\\award{mod}.brres.d");
                        extractAwardsSZS = new Process();
                        extractAwardsSZS.StartInfo = extractFile(tempPath + $"\\Award.d\\{target.abbrev}{mod}.brres", "");
                        extractAwardsSZS.Start();
                        extractAwardsSZS.WaitForExit();
                        extractAwardsSZS.Dispose();
                        foreach (string dir in Directory.GetDirectories(tempPath + $"\\Award.d\\{target.abbrev}{mod}.brres.d"))
                        {
                            string matchingFolder = Path.GetFileName(dir);
                            if (matchingFolder != "3DModels(NW4R)" && matchingFolder != "Textures(NW4R)")
                            {
                                Directory.Move(dir, folderPath + $"\\award{mod}.brres.d\\{matchingFolder}");
                            }
                        }
                    }
                }
            }
            globalProgress.Value++;

            // Get Icons
            progressLabel.Text = "Fetching Icons from Race.szs...";
            string iconPath = mkwFilePath + "\\Scene\\UI\\Race.szs";
            if (File.Exists(iconPath))
            {
                File.Copy(iconPath, tempPath + "\\Icons.szs");
                Process extractIconSZS = new Process();
                extractIconSZS.StartInfo = extractFile(tempPath + "\\Icons.szs", "");
                extractIconSZS.Start();
                extractIconSZS.WaitForExit();
                extractIconSZS.Dispose();

                foreach (string iconName in target.iconNames)
                {
                    string timgPath = tempPath + "\\Icons.d\\game_image\\timg";
                    if (File.Exists(timgPath + $"\\st_{iconName}_32x32.tpl"))
                    {
                        File.Copy(timgPath + $"\\st_{iconName}_32x32.tpl", folderPath + $"\\st_icon_32x32.tpl");
                    }
                    if (File.Exists(timgPath + $"\\tt_{iconName}_64x64.tpl"))
                    {
                        File.Copy(timgPath + $"\\tt_{iconName}_64x64.tpl", folderPath + $"\\tt_icon_64x64.tpl");
                    }
                }
            }
            globalProgress.Value++;

            // Get Character Name
            progressLabel.Text = $"Fetching Character Name from Race_{targetLanguageBox.Text}.szs...";
            string uiLanguagePath = mkwFilePath + $"\\Scene\\UI\\Race_{targetLanguageBox.Text}.szs";
            if (File.Exists(uiLanguagePath))
            {
                File.Copy(uiLanguagePath, tempPath + "\\IconText.szs");
                Process extractIconSZS = new Process();
                extractIconSZS.StartInfo = extractFile(tempPath + "\\IconText.szs", "");
                extractIconSZS.Start();
                extractIconSZS.WaitForExit();
                extractIconSZS.Dispose();

                if (File.Exists(tempPath + "\\IconText.d\\message\\Common.bmg"))
                {
                    Process extractIconTextSZS = new Process();
                    extractIconTextSZS.StartInfo = extractBMG(tempPath + "\\IconText.d\\message\\Common.bmg", "");
                    extractIconTextSZS.Start();
                    extractIconTextSZS.WaitForExit();
                    extractIconTextSZS.Dispose();

                    string[] bmgLines = File.ReadAllLines(tempPath + "\\IconText.d\\message\\Common.txt");
                    for (int i = 0; i < bmgLines.Length; i++)
                    {
                        string targetOffset = $"  {target.bmgOffset}\t= ";
                        if (bmgLines[i].StartsWith(targetOffset))
                        {
                            Directory.CreateDirectory(folderPath + "\\name");
                            File.WriteAllText(folderPath + "\\name\\" + bmgLines[i].Substring(targetOffset.Length), "");
                        }
                    }

                }
            }
            globalProgress.Value++;

            // Create Brres Files
            progressLabel.Text = "Building Brres Files...";
            foreach (string dir in Directory.GetDirectories(folderPath))
            {
                if (dir.EndsWith(".brres.d"))
                {
                    if (Directory.GetFiles(dir).Length > 0 || Directory.GetDirectories(dir).Length > 0)
                    {
                        Process createBrres = new Process();
                        createBrres.StartInfo = createFile(dir, "--brres --no-compress ");
                        createBrres.Start();
                        createBrres.WaitForExit();
                        createBrres.Dispose();
                    }
                    Directory.Delete(dir, true);
                }
            }
            globalProgress.Value++;

            progressLabel.Text = "Building Sample Model...";
            if (File.Exists(path)) File.Delete(path);
            Process createSzs = new Process();
            createSzs.StartInfo = createFile(folderPath, "");
            createSzs.Start();
            createSzs.WaitForExit();
            createSzs.Dispose();
            Directory.Delete(folderPath, true);
            globalProgress.Value++;

            // Delete Temp folder when all processing is done
            Directory.Delete(tempPath, true);
            progressLabel.Text = "Done!";
            globalProgress.Value++;
        }

        private void CreateFilesFromSample(string[] paths)
        {
            // First check to see if characters being replaced are valid
            if (paths.Length > 1)
            {
                foreach (string path in paths)
                {
                    if (SampleCharacterName(path) == null) return;
                }
            }

            // Create Temp Folders
            globalProgress.Value = 0;
            progressLabel.Text = "Initializing...";
            string tempPath = AppDomain.CurrentDomain.BaseDirectory + "\\Temp";
            string outputFolder = paths[0] + ".output";
            if (paths.Length > 1) outputFolder = Path.GetDirectoryName(paths[0]) + "\\Multisample.output";

            string[] allIconLocations = new string[10]
            {
                "Award, award",
                "Channel, control",
                "Event, button",
                "Globe, button",
                "MenuMulti, button|control",
                "MenuOther",
                "MenuSingle, button|control",
                "Present",
                "Race, game_image|result",
                "Title"
            };
            string[] allKartModifiers = new string[2] { "", "_BT" };
            globalProgress.Maximum = 4 + allIconLocations.Length;
            globalProgress.Value++;

            progressLabel.Text = "Preparing output folder...";
            if (Directory.Exists(outputFolder)) Directory.Delete(outputFolder, true);
            Directory.CreateDirectory(outputFolder);
            Directory.CreateDirectory(outputFolder + "\\files");
            Directory.CreateDirectory(outputFolder + "\\files\\Race");
            Directory.CreateDirectory(outputFolder + "\\files\\Race\\Kart");
            Directory.CreateDirectory(outputFolder + "\\files\\Demo");
            Directory.CreateDirectory(outputFolder + "\\files\\Scene");
            Directory.CreateDirectory(outputFolder + "\\files\\Scene\\Model");
            Directory.CreateDirectory(outputFolder + "\\files\\Scene\\Model\\Kart");
            Directory.CreateDirectory(outputFolder + "\\files\\Scene\\UI");
            globalProgress.Value++;

            foreach (string iconLocation in allIconLocations)
            {
                string name = iconLocation.Split(", ")[0];
                progressLabel.Text = $"Extracting Icons {name}.szs and Text {name}_{targetLanguageBox.Text}.szs...";
                if (File.Exists(mkwFilePath + $"\\Scene\\UI\\{name}.szs"))
                {
                    File.Copy(mkwFilePath + $"\\Scene\\UI\\{name}.szs", outputFolder + $"\\files\\Scene\\UI\\{name}.szs", true);
                    Process extractIcon = new Process();
                    extractIcon.StartInfo = extractFile(outputFolder + $"\\files\\Scene\\UI\\{name}.szs", "");
                    extractIcon.Start();
                    extractIcon.WaitForExit();
                    extractIcon.Dispose();
                    File.Delete(outputFolder + $"\\files\\Scene\\UI\\{name}.szs");
                }
                if (File.Exists(mkwFilePath + $"\\Scene\\UI\\{name}_{targetLanguageBox.Text}.szs"))
                {
                    File.Copy(mkwFilePath + $"\\Scene\\UI\\{name}_{targetLanguageBox.Text}.szs", outputFolder + $"\\files\\Scene\\UI\\{name}_{targetLanguageBox.Text}.szs", true);
                    Process extractIcon = new Process();
                    extractIcon.StartInfo = extractFile(outputFolder + $"\\files\\Scene\\UI\\{name}_{targetLanguageBox.Text}.szs", "");
                    extractIcon.Start();
                    extractIcon.WaitForExit();
                    extractIcon.Dispose();
                    File.Delete(outputFolder + $"\\files\\Scene\\UI\\{name}_{targetLanguageBox.Text}.szs");
                }
                globalProgress.Value++;
            }

            // Get the common.bmg in Race_U.szs
            if (File.Exists(outputFolder + $"\\files\\Scene\\UI\\Race_{targetLanguageBox.Text}.d\\message\\Common.bmg"))
            {
                string bmgPath = AppDomain.CurrentDomain.BaseDirectory + "\\Common";
                File.Copy(outputFolder + $"\\files\\Scene\\UI\\Race_{targetLanguageBox.Text}.d\\message\\Common.bmg", bmgPath + ".bmg", true);
                if (File.Exists(bmgPath + ".txt")) File.Delete(bmgPath + ".txt");
                Process extractBMGMessage = new Process();
                extractBMGMessage.StartInfo = extractBMG(bmgPath + ".bmg", "");
                extractBMGMessage.Start();
                extractBMGMessage.WaitForExit();
                extractBMGMessage.Dispose();
                File.Delete(bmgPath + ".bmg");
            }

            // Copy and Extract Driver and Award Files
            Process extract = new Process();
            progressLabel.Text = "Extracting Driver.szs...";
            if (File.Exists(mkwFilePath + $"\\Scene\\Model\\Driver.szs"))
            {
                File.Copy(mkwFilePath + $"\\Scene\\Model\\Driver.szs", outputFolder + $"\\files\\Scene\\Model\\Driver.szs", true);
                extract = new Process();
                extract.StartInfo = extractFile(outputFolder + $"\\files\\Scene\\Model\\Driver.szs", "");
                extract.Start();
                extract.WaitForExit();
                extract.Dispose();
                File.Delete(outputFolder + $"\\files\\Scene\\Model\\Driver.szs");
            }
            globalProgress.Value++;
            progressLabel.Text = "Extracting Award.szs...";
            if (File.Exists(mkwFilePath + $"\\Demo\\Award.szs"))
            {
                File.Copy(mkwFilePath + $"\\Demo\\Award.szs", outputFolder + $"\\files\\Demo\\Award.szs", true);
                extract = new Process();
                extract.StartInfo = extractFile(outputFolder + $"\\files\\Demo\\Award.szs", "");
                extract.Start();
                extract.WaitForExit();
                extract.Dispose();
                File.Delete(outputFolder + $"\\files\\Demo\\Award.szs");
            }
            globalProgress.Value++;

            foreach (string path in paths)
            {
                string charName = targetCharBox.Text;
                if (paths.Length > 1) charName = SampleCharacterName(path);
                MKW_Character target = characters[charName];

                // Get list of vehicles to check
                List<string> keysToCheck = new List<string>();
                foreach (string key in vehicles.Keys)
                {
                    if (vehicles[key].size == target.size)
                    {
                        if (!vehicleGeneratorList.GetItemChecked(1))
                        {
                            // Skip colored vehicles if the generator doesn't allow them
                            if (key.EndsWith("Blue") || key.EndsWith("Red")) continue;
                        }

                        if (vehicleGeneratorList.Items.Contains($"{vehicles[key].type}s"))
                        {
                            int index = vehicleGeneratorList.Items.IndexOf($"{vehicles[key].type}s");
                            if (vehicleGeneratorList.GetItemChecked(index))
                            {
                                keysToCheck.Add(key);
                            }
                        }
                    }
                }

                globalProgress.Value = 0;
                globalProgress.Maximum = 4 + allKartModifiers.Length + vehicleTypes.Count + keysToCheck.Count + allIconLocations.Length;

                // Extract sample to Temp folder
                if (Directory.Exists(tempPath)) Directory.Delete(tempPath, true);
                Directory.CreateDirectory(tempPath);
                progressLabel.Text = "Extracting sample";
                File.Copy(path, tempPath + "\\sample.szs");
                Process extractSampleSZS = new Process();
                extractSampleSZS.StartInfo = extractFile(tempPath + "\\sample.szs", "");
                extractSampleSZS.Start();
                extractSampleSZS.WaitForExit();
                extractSampleSZS.Dispose();
                File.Delete(tempPath + "\\sample.szs");

                if (File.Exists(tempPath + "\\sample.d\\driver.brres"))
                {
                    // Extract driver brres
                    Process extractDriver = new Process();
                    extractDriver.StartInfo = extractFile(tempPath + "\\sample.d\\driver.brres", "");
                    extractDriver.Start();
                    extractDriver.WaitForExit();
                    extractDriver.Dispose();
                    File.Delete(tempPath + "\\sample.d\\driver.brres");
                }
                globalProgress.Value++;

                // #############################################################################################
                // #################################### VEHICLE TYPE BRRES #####################################
                // #############################################################################################

                foreach (string v_type in vehicleTypes)
                {
                    string type = v_type.ToLower().Replace(" ", "_");
                    progressLabel.Text = $"Creating '{v_type}' BRRES for {charName}...";
                    if (File.Exists(tempPath + $"\\sample.d\\driving_{type}.brres"))
                    {
                        // Extract this brres and use it as a base
                        File.Copy(tempPath + $"\\sample.d\\driving_{type}.brres", tempPath + $"\\anims_{type}.brres", true);
                        Process extractType = new Process();
                        extractType.StartInfo = extractFile(tempPath + $"\\anims_{type}.brres", "");
                        extractType.Start();
                        extractType.WaitForExit();
                        extractType.Dispose();
                        File.Delete(tempPath + $"\\{type}.brres");
                    }
                    else Directory.CreateDirectory(tempPath + $"\\anims_{type}.brres.d");

                    // Create final folder to move all directories to
                    Directory.CreateDirectory(tempPath + $"\\{type}_brres.d");

                    // If a default driver.brres exists, Extract it and move it's files into the base
                    if (Directory.Exists(tempPath + "\\sample.d\\driver.brres.d"))
                    {
                        foreach (string dir in Directory.GetDirectories(tempPath + "\\sample.d\\driver.brres.d"))
                        {
                            string matchingFolder = Path.GetFileName(dir);
                            if (matchingFolder == $"LOD_{type}")
                            {
                                // move LOD files into 3DModels Folder
                                foreach (string file in Directory.GetFiles(dir))
                                {
                                    if (!Directory.Exists(tempPath + $"\\{type}.brres.d\\3DModels(NW4R)")) Directory.CreateDirectory(tempPath + $"\\{type}.brres.d\\3DModels(NW4R)");
                                    File.Copy(file, tempPath + $"\\{type}.brres.d\\3DModels(NW4R)\\{Path.GetFileName(file)}");
                                }
                            }
                            else if (!matchingFolder.StartsWith("LOD_"))
                            {
                                CopyDirectory(dir, tempPath + $"\\{type}.brres.d\\{matchingFolder}", true);
                            }
                        }
                    }
                    foreach (string dir in Directory.GetDirectories(tempPath + $"\\anims_{type}.brres.d"))
                    {
                        string matchingFolder = Path.GetFileName(dir);
                        CopyDirectory(dir, tempPath + $"\\{type}.brres.d\\{matchingFolder}", true);
                    }
                    Process createBrres = new Process();
                    createBrres.StartInfo = createFile(tempPath + $"\\{type}.brres.d", "--brres --no-compress ");
                    createBrres.Start();
                    createBrres.WaitForExit();
                    createBrres.Dispose();
                    Directory.Delete(tempPath + $"\\anims_{type}.brres.d", true);
                    Directory.Delete(tempPath + $"\\{type}.brres.d", true);
                    globalProgress.Value++;
                }

                // #############################################################################################
                // ##################################### AWARD CEREMONY SZS ####################################
                // #############################################################################################

                progressLabel.Text = $"Creating new {charName} brres file(s) for Award.szs...";
                string awardPath = outputFolder + "\\files\\Demo\\Award.d";
                if (Directory.Exists(awardPath))
                {
                    // Get replacement targets
                    List<string> complexAwardSampling = new List<string>();
                    if (File.Exists(awardPath + $"\\{target.abbrev}.brres")) complexAwardSampling.Add("kart|");
                    else Debug.WriteLine($"Failed to find {target.abbrev}.brres in Award.szs");
                    if (File.Exists(awardPath + $"\\{target.abbrev}3.brres")) complexAwardSampling.Add("bike|3");
                    else Debug.WriteLine($"Failed to find {target.abbrev}3.brres in Award.szs");

                    // Extract Sample's Award Animations.
                    foreach (string award in complexAwardSampling)
                    {
                        string check = award.Split("|")[0];
                        string mod = award.Split("|")[1];

                        if (File.Exists(tempPath + $"\\sample.d\\award{mod}.brres"))
                        {
                            extract = new Process();
                            extract.StartInfo = extractFile(tempPath + $"\\sample.d\\award{mod}.brres", "");
                            extract.Start();
                            extract.WaitForExit();
                            extract.Dispose();

                            if (Directory.Exists(tempPath + "\\sample.d\\driver.brres.d"))
                            {
                                // ############################### Use driver to make all award files ###############################
                                Directory.CreateDirectory(tempPath + "\\AwardFromDriver.brres.d");
                                foreach (string modelDir in Directory.GetDirectories(tempPath + "\\sample.d\\driver.brres.d"))
                                {
                                    string matchingFolder = Path.GetFileName(modelDir);
                                    if (matchingFolder.EndsWith("(NW4R)")) CopyDirectory(modelDir, tempPath + $"\\AwardFromDriver.brres.d\\{matchingFolder}", true);
                                }
                                foreach (string animDir in Directory.GetDirectories(tempPath + $"\\sample.d\\award{mod}.brres.d"))
                                {
                                    string matchingFolder = Path.GetFileName(animDir);
                                    CopyDirectory(animDir, tempPath + $"\\AwardFromDriver.brres.d\\{matchingFolder}", true);
                                }
                                // Build DriverAward.brres
                                Process createAwardBrres = new Process();
                                createAwardBrres.StartInfo = createFile(tempPath + "\\AwardFromDriver.brres.d", "--brres --no-compress ");
                                createAwardBrres.Start();
                                createAwardBrres.WaitForExit();
                                createAwardBrres.Dispose();
                                Directory.Delete(tempPath + "\\AwardFromDriver.brres.d", true);

                                // Copy new Brres into extracted Award.szs
                                File.Move(tempPath + "\\AwardFromDriver.brres", awardPath + $"\\{target.abbrev}{mod}.brres", true);

                                // Niche exception, if there's no award model for a bike model when replacing a complex character, copy the file over that as well
                                if (!File.Exists(tempPath + $"\\sample.d\\award3.brres") && File.Exists(awardPath + $"\\{target.abbrev}3.brres"))
                                {
                                    File.Copy(awardPath + $"\\{target.abbrev}{mod}.brres", awardPath + $"\\{target.abbrev}3.brres", true);
                                }
                            }
                            else
                            {
                                // ############################### Check for individual brres files ###############################
                                if (File.Exists(tempPath + $"\\sample.d\\driving_{check}.brres"))
                                {
                                    File.Copy(tempPath + $"\\sample.d\\driving_{check}.brres", tempPath + $"\\{target.abbrev}{mod}.brres");
                                    Process extractBrres = new Process();
                                    extractBrres.StartInfo = extractFile(tempPath + $"\\{target.abbrev}{mod}.brres", "");
                                    extractBrres.Start();
                                    extractBrres.WaitForExit();
                                    extractBrres.Dispose();
                                    File.Delete(tempPath + $"\\{target.abbrev}{mod}.brres");
                                    foreach (string dir in Directory.GetDirectories(tempPath + $"\\{target.abbrev}{mod}.brres.d"))
                                    {
                                        if (!dir.EndsWith("3DModels(NW4R)") && !dir.EndsWith("Textures(NW4R)"))
                                        {
                                            Directory.Delete(dir, true);
                                        }
                                        else if (dir.EndsWith("3DModels(NW4R)"))
                                        {
                                            if (File.Exists(dir + "\\model_lod")) File.Delete(dir + "\\model_lod");
                                        }
                                    }
                                }
                                else Directory.CreateDirectory(tempPath + $"\\{target.abbrev}{mod}.brres.d");

                                foreach (string animDir in Directory.GetDirectories(tempPath + $"\\sample.d\\award{mod}.brres.d"))
                                {
                                    string matchingFolder = Path.GetFileName(animDir);
                                    CopyDirectory(animDir, tempPath + $"\\{target.abbrev}{mod}.brres.d\\{matchingFolder}", true);
                                }

                                Process createAwardBrres = new Process();
                                createAwardBrres.StartInfo = createFile(tempPath + $"\\{target.abbrev}{mod}.brres.d", "--brres --no-compress ");
                                createAwardBrres.Start();
                                createAwardBrres.WaitForExit();
                                createAwardBrres.Dispose();
                                Directory.Delete(tempPath + $"\\{target.abbrev}{mod}.brres.d", true);
                                File.Move(tempPath + $"\\{target.abbrev}{mod}.brres", awardPath + $"\\{target.abbrev}{mod}.brres", true);
                            }
                        }
                        else Debug.WriteLine($"Failed to find award{mod}.brres file in the sample, continuing...");
                    }
                }
                else MessageBox.Show("Failed to find Award.d to import model into, continuing...");
                globalProgress.Value++;

                // #############################################################################################
                // ###################################### CHAR SELECT SZS ######################################
                // #############################################################################################

                progressLabel.Text = $"Creating new {charName} brres file for Driver.szs...";
                string driverPath = outputFolder + "\\files\\Scene\\Model\\Driver.d";
                if (Directory.Exists(driverPath))
                {
                    // Extract Sample's Character Select Animations
                    if (File.Exists(tempPath + "\\sample.d\\select_char.brres"))
                    {
                        extract = new Process();
                        extract.StartInfo = extractFile(tempPath + "\\sample.d\\select_char.brres", "");
                        extract.Start();
                        extract.WaitForExit();
                        extract.Dispose();

                        // Copy folders into a new brres folder 
                        Directory.CreateDirectory(tempPath + "\\DriverMenu.brres.d");
                        foreach (string animDir in Directory.GetDirectories(tempPath + $"\\sample.d\\select_char.brres.d"))
                        {
                            string matchingFolder = Path.GetFileName(animDir);
                            CopyDirectory(animDir, tempPath + $"\\DriverMenu.brres.d\\{matchingFolder}", true);
                        }
                        if (Directory.Exists(tempPath + "\\sample.d\\driver.brres.d"))
                        {
                            foreach (string dir in Directory.GetDirectories(tempPath + "\\sample.d\\driver.brres.d"))
                            {
                                string matchingFolder = Path.GetFileName(dir);
                                if (!matchingFolder.StartsWith("LOD_")) CopyDirectory(dir, tempPath + $"\\DriverMenu.brres.d\\{matchingFolder}", true);
                            }
                        }

                        // Build DriverMenu.brres
                        Process createAwardModel = new Process();
                        createAwardModel.StartInfo = createFile(tempPath + "\\DriverMenu.brres.d", "--brres --no-compress ");
                        createAwardModel.Start();
                        createAwardModel.WaitForExit();
                        createAwardModel.Dispose();
                        Directory.Delete(tempPath + "\\DriverMenu.brres.d", true);

                        // Copy new Brres into extracted Driver.szs
                        if (File.Exists(driverPath + $"\\{target.abbrev}.brres"))
                        {
                            File.Copy(tempPath + "\\DriverMenu.brres", driverPath + $"\\{target.abbrev}.brres", true);
                        }
                        else if (File.Exists(driverPath + $"\\{target.abbrev}_menu.brres"))
                        {
                            File.Copy(tempPath + "\\DriverMenu.brres", driverPath + $"\\{target.abbrev}_menu.brres", true);
                        }
                    }
                    else Debug.WriteLine("Failed to find select_char.brres in sample.");
                }
                else Debug.WriteLine("Failed to find Driver.d to import model into, continuing...");
                globalProgress.Value++;

                // #############################################################################################
                // ########################################## VEHICLES #########################################
                // #############################################################################################

                // First create new AllKart Folders
                progressLabel.Text = $"Creating new allkart.szs folder(s) for {charName}...";
                bool BT_Allowed = vehicleGeneratorList.GetItemChecked(1);
                Directory.CreateDirectory(tempPath + $"\\Scene\\Model\\Kart\\{target.abbrev}-allkart.d");
                if (BT_Allowed) Directory.CreateDirectory(tempPath + $"\\Scene\\Model\\Kart\\{target.abbrev}-allkart_BT.d");
                if (File.Exists(tempPath + "\\sample.d\\select_kart.brres"))
                {
                    File.Copy(tempPath + "\\sample.d\\select_kart.brres", tempPath + $"\\Scene\\Model\\Kart\\{target.abbrev}-allkart.d\\driver_anim.brres", true);
                    if (BT_Allowed) File.Copy(tempPath + "\\sample.d\\select_kart.brres", tempPath + $"\\Scene\\Model\\Kart\\{target.abbrev}-allkart_BT.d\\driver_anim.brres", true);
                }
                globalProgress.Value++;

                // Extract every vehicle model for the target character, and put the matching type driver model and vehicle inside.
                for (int i = 0; i < keysToCheck.Count; i++)
                {
                    progressLabel.Text = $"Editing {charName}'s {keysToCheck[i]} files...";
                    if (File.Exists(tempPath + "\\vehicle.szs")) File.Delete(tempPath + "\\vehicle.szs");
                    if (Directory.Exists(tempPath + "\\vehicle.d")) Directory.Delete(tempPath + "\\vehicle.d", true);

                    // Copy Vehicle file from mkw Race folder to the temporary folder
                    string vehiclePath = mkwFilePath + $"\\Race\\Kart\\{vehicles[keysToCheck[i]].abbrev}-{target.abbrev}.szs";
                    if (File.Exists(vehiclePath)) File.Copy(vehiclePath, tempPath + "\\vehicle.szs");
                    else
                    {
                        Debug.WriteLine("Failed to find vehicle: " + vehiclePath);
                        continue;
                    }

                    // Move and extract this vehicle
                    File.Copy(vehiclePath, tempPath + "\\vehicle.szs", true);
                    Process extractVehicle = new Process();
                    extractVehicle.StartInfo = extractFile(tempPath + "\\vehicle.szs", "");
                    extractVehicle.Start();
                    extractVehicle.WaitForExit();
                    extractVehicle.Dispose();
                    File.Delete(tempPath + "\\vehicle.szs");

                    // Import sample driver models into extracted vehicle
                    string type = vehicles[keysToCheck[i]].type.ToLower().Replace(" ", "_");
                    if (File.Exists(tempPath + $"\\{type}.brres"))
                    {
                        File.Copy(tempPath + $"\\{type}.brres", tempPath + "\\vehicle.d\\driver_model.brres", true);
                    }
                    else Debug.WriteLine("Failed to find sample driver type: " + tempPath + $"\\{type}.brres");

                    // Extract vehicle to make in-game and menu brres files.
                    string sampleVehiclePath = tempPath + $"\\sample.d\\vehicles\\{keysToCheck[i].Replace(" ", "_")}.brres";
                    if (File.Exists(sampleVehiclePath))
                    {
                        // Build allkart copy and move it to the right folder
                        extractVehicle = new Process();
                        extractVehicle.StartInfo = extractFile(sampleVehiclePath, "");
                        extractVehicle.Start();
                        extractVehicle.WaitForExit();
                        extractVehicle.Dispose();
                        File.Delete(sampleVehiclePath);
                        Directory.CreateDirectory(tempPath + "\\gameVehicle.brres.d");
                        Directory.CreateDirectory(tempPath + "\\allkartVehicle.brres.d");
                        foreach (string dir in Directory.GetDirectories(sampleVehiclePath + ".d"))
                        {
                            string matchingFolder = Path.GetFileName(dir);
                            switch (matchingFolder)
                            {
                                case "Textures(NW4R)":
                                    if (!Directory.GetDirectories(sampleVehiclePath + ".d").Contains(sampleVehiclePath + ".d\\Menu_Textures(NW4R)"))
                                    {
                                        CopyDirectory(dir, tempPath + "\\allkartVehicle.brres.d\\Textures(NW4R)", true);
                                    }
                                    CopyDirectory(dir, tempPath + "\\gameVehicle.brres.d\\Textures(NW4R)", true);
                                    break;
                                case "Menu_3DModels(NW4R)":
                                    CopyDirectory(dir, tempPath + "\\allkartVehicle.brres.d\\3DModels(NW4R)", true);
                                    break;
                                case "Menu_Textures(NW4R)":
                                    CopyDirectory(dir, tempPath + "\\allkartVehicle.brres.d\\Textures(NW4R)", true);
                                    break;
                                default:
                                    CopyDirectory(dir, tempPath + $"\\gameVehicle.brres.d\\{matchingFolder}", true);
                                    break;
                            }
                        }
                        // Build In-Game Vehicle
                        Process buildVehicle = new Process();
                        buildVehicle.StartInfo = createFile(tempPath + "\\gameVehicle.brres.d", "--brres --no-compress ");
                        buildVehicle.Start();
                        buildVehicle.WaitForExit();
                        buildVehicle.Dispose();
                        Directory.Delete(tempPath + "\\gameVehicle.brres.d", true);
                        File.Move(tempPath + "\\gameVehicle.brres", tempPath + "\\vehicle.d\\kart_model.brres", true);

                        // Build allkart Copy Vehicle
                        buildVehicle = new Process();
                        buildVehicle.StartInfo = createFile(tempPath + "\\allkartVehicle.brres.d", "--brres --no-compress ");
                        buildVehicle.Start();
                        buildVehicle.WaitForExit();
                        buildVehicle.Dispose();
                        Directory.Delete(tempPath + "\\allkartVehicle.brres.d", true);
                        if ((keysToCheck[i].EndsWith("Blue") || keysToCheck[i].EndsWith("Red")) && BT_Allowed)
                        {
                            // Move this vehicle into BT allkart folder instead
                            File.Move(tempPath + "\\allkartVehicle.brres", tempPath + $"\\Scene\\Model\\Kart\\{target.abbrev}-allkart_BT.d\\{vehicles[keysToCheck[i]].abbrev}.brres");
                        }
                        else File.Move(tempPath + "\\allkartVehicle.brres", tempPath + $"\\Scene\\Model\\Kart\\{target.abbrev}-allkart.d\\{vehicles[keysToCheck[i]].abbrev}.brres");
                    }
                    else Debug.WriteLine("Failed to find sample vehicle: " + sampleVehiclePath);

                    // Rebuild vehicle and move it to output folder
                    Process buildSZS = new Process();
                    buildSZS.StartInfo = createFile(tempPath + "\\vehicle.d", "");
                    buildSZS.Start();
                    buildSZS.WaitForExit();
                    buildSZS.Dispose();
                    File.Copy(tempPath + "\\vehicle.szs", outputFolder + $"\\files\\Race\\Kart\\{vehicles[keysToCheck[i]].abbrev}-{target.abbrev}.szs", true);
                    if (vehicleGeneratorList.GetItemChecked(0))
                    {
                        File.Copy(tempPath + "\\vehicle.szs", outputFolder + $"\\files\\Race\\Kart\\{vehicles[keysToCheck[i]].abbrev}-{target.abbrev}_4.szs", true);
                    }
                    globalProgress.Value++;
                }

                // Build AllKart folders and move them to output folder
                foreach (string mod in allKartModifiers)
                {
                    progressLabel.Text = $"Building {target.abbrev}-allkart{mod}.szs...";
                    Process buildAllKart = new Process();
                    buildAllKart.StartInfo = createFile(tempPath + $"\\Scene\\Model\\Kart\\{target.abbrev}-allkart{mod}.d", "");
                    buildAllKart.Start();
                    buildAllKart.WaitForExit();
                    buildAllKart.Dispose();
                    File.Move(tempPath + $"\\Scene\\Model\\Kart\\{target.abbrev}-allkart{mod}.szs", outputFolder + $"\\files\\Scene\\Model\\Kart\\{target.abbrev}-allkart{mod}.szs", true);
                    globalProgress.Value++;
                }

                // Duplicate and extract each icon file and replace the icons that already exist
                foreach (string location in allIconLocations)
                {
                    string[] loc = location.Split(", ", StringSplitOptions.TrimEntries);
                    string smallIconPath = tempPath + $"\\sample.d\\st_icon_32x32.tpl";
                    string largeIconPath = tempPath + $"\\sample.d\\tt_icon_64x64.tpl";
                    progressLabel.Text = $"Importing icon(s) into {loc[0]}.szs...";
                    if (loc.Length <= 1)
                    {
                        globalProgress.Value++;
                        continue;
                    }
                    string[] iconFolders = loc[1].Split("|", StringSplitOptions.TrimEntries);
                    foreach (string folderName in iconFolders)
                    {
                        foreach (string driverName in target.iconNames)
                        {
                            string timgPath = outputFolder + $"\\files\\Scene\\UI\\{loc[0]}.d\\{folderName}\\timg";
                            if (Directory.Exists(timgPath))
                            {
                                if (File.Exists(timgPath + $"\\st_{driverName}_32x32.tpl") && File.Exists(smallIconPath))
                                {
                                    File.Copy(smallIconPath, timgPath + $"\\st_{driverName}_32x32.tpl", true);
                                }
                                if (File.Exists(timgPath + $"\\tt_{driverName}_64x64.tpl") && File.Exists(largeIconPath))
                                {
                                    File.Copy(largeIconPath, timgPath + $"\\tt_{driverName}_64x64.tpl", true);
                                }
                            }
                            else Debug.WriteLine("Failed to find icon's timg folder at: " + timgPath);
                        }
                    }
                    globalProgress.Value++;
                }

                // Overwrite the name in the common.txt in Temp
                if (Directory.Exists(tempPath + "\\sample.d\\name"))
                {
                    foreach (string namePath in Directory.GetFiles(tempPath + "\\sample.d\\name"))
                    {
                        OverwriteName(AppDomain.CurrentDomain.BaseDirectory + "\\Common.txt", Path.GetFileName(namePath), target);
                    }
                }
            }

            globalProgress.Value = 0;
            globalProgress.Maximum = 3 + allIconLocations.Length;

            // Repack all the brres files in the output folder
            Process create = new Process();
            progressLabel.Text = "Repacking driver.szs...";
            if (Directory.Exists(outputFolder + "\\files\\Scene\\Model\\Driver.d"))
            {
                create = new Process();
                create.StartInfo = createFile(outputFolder + "\\files\\Scene\\Model\\Driver.d", "");
                create.Start();
                create.WaitForExit();
                create.Dispose();
                Directory.Delete(outputFolder + "\\files\\Scene\\Model\\Driver.d", true);
            }
            globalProgress.Value++;

            progressLabel.Text = "Repacking Award.szs...";
            if (Directory.Exists(outputFolder + "\\files\\Demo\\Award.d"))
            {
                create = new Process();
                create.StartInfo = createFile(outputFolder + "\\files\\Demo\\Award.d", "");
                create.Start();
                create.WaitForExit();
                create.Dispose();
                Directory.Delete(outputFolder + "\\files\\Demo\\Award.d", true);
            }
            globalProgress.Value++;

            // Create new BMG to insert into the language szs files
            progressLabel.Text = "Creating Common.bmg...";
            if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\Common.txt"))
            {
                create = new Process();
                create.StartInfo = createBMG(AppDomain.CurrentDomain.BaseDirectory + "\\Common.txt", "");
                create.Start();
                create.WaitForExit();
                create.Dispose();
                File.Delete(AppDomain.CurrentDomain.BaseDirectory + "\\Common.txt");
            }

            globalProgress.Value++;
            foreach (string iconLocation in allIconLocations)
            {
                string name = iconLocation.Split(", ")[0];
                progressLabel.Text = $"Repacking Icons SZS: {name}.szs and Name SZS: {name}_{targetLanguageBox.Text}.szs...";
                if (Directory.Exists(outputFolder + $"\\files\\Scene\\UI\\{name}.d"))
                {
                    create = new Process();
                    create.StartInfo = createFile(outputFolder + $"\\files\\Scene\\UI\\{name}.d", "");
                    create.Start();
                    create.WaitForExit();
                    create.Dispose();
                    Directory.Delete(outputFolder + $"\\files\\Scene\\UI\\{name}.d", true);
                }
                if (Directory.Exists(outputFolder + $"\\files\\Scene\\UI\\{name}_{targetLanguageBox.Text}.d"))
                {
                    if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\Common.bmg") && 
                        File.Exists(outputFolder + $"\\files\\Scene\\UI\\{name}_{targetLanguageBox.Text}.d\\message\\Common.bmg"))
                    {
                        File.Copy(AppDomain.CurrentDomain.BaseDirectory + "\\Common.bmg", outputFolder + $"\\files\\Scene\\UI\\{name}_{targetLanguageBox.Text}.d\\message\\Common.bmg", true);
                    }
                    create = new Process();
                    create.StartInfo = createFile(outputFolder + $"\\files\\Scene\\UI\\{name}_{targetLanguageBox.Text}.d", "");
                    create.Start();
                    create.WaitForExit();
                    create.Dispose();
                    Directory.Delete(outputFolder + $"\\files\\Scene\\UI\\{name}_{targetLanguageBox.Text}.d", true);
                }
                globalProgress.Value++;
            }

            // Clear leftover files            if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\Common.bmg"))
                File.Delete(AppDomain.CurrentDomain.BaseDirectory + "\\Common.bmg");
            if (Directory.Exists(tempPath))
                Directory.Delete(tempPath, true);

            // Open output folder when finished
            progressLabel.Text = "Done!";
            Process openFolder = new Process();
            try
            {
                openFolder.StartInfo = new ProcessStartInfo()
                {
                    UseShellExecute = true,
                    FileName = outputFolder
                };
                openFolder.Start();
            }
            catch { }
            openFolder.Dispose();
        }
        private void RefreshVehicleGenerationList()
        {
            vehicleGeneratorList.Items.Clear();
            vehicleGeneratorList.Items.Add("Multiplayer Vehicle Duplicates", true);
            vehicleGeneratorList.Items.Add("Colored Standard Vehicles", true);
            foreach (string vehicleType in vehicleTypes)
            {
                vehicleGeneratorList.Items.Add($"{vehicleType}s", true);
            }
        }

        private void ParseMKWParameters()
        {
            string paramPath = AppDomain.CurrentDomain.BaseDirectory + "\\MKW_Parameters.txt";

            if (File.Exists(paramPath))
            {
                vehicleTypes = new List<string>();
                characters = new Dictionary<string, MKW_Character>();
                vehicles = new Dictionary<string, MKW_Vehicle>();
                StreamReader read = new StreamReader(paramPath);
                string line = read.ReadLine();
                if (line == null) return;
                line = line.Split("//", StringSplitOptions.TrimEntries)[0];

                while (line != null && !line.Contains(" CHARACTERS "))
                {
                    // Read until reaching CHARACTERS
                    line = read.ReadLine();
                    if (line == null) return;
                    line = line.Split("//", StringSplitOptions.TrimEntries)[0];
                }

                line = read.ReadLine();
                if (line == null) return;
                line = line.Split("//", StringSplitOptions.TrimEntries)[0];

                while (line != null && !line.Contains(" VEHICLES "))
                {
                    // Read characters until reaching VEHICLES
                    if (!string.IsNullOrEmpty(line))
                    {
                        try
                        {
                            string[] chara_parameters = line.Split("|", StringSplitOptions.TrimEntries);
                            MKW_Character chara = new MKW_Character();
                            chara.abbrev = chara_parameters[1];
                            chara.iconNames = chara_parameters[2].Split(",", StringSplitOptions.TrimEntries);
                            chara.size = chara_parameters[3];
                            if (chara_parameters[4] == "Complex") chara.isComplex = true;
                            else chara.isComplex = false;
                            if (chara_parameters.Length > 5) chara.bmgOffset = chara_parameters[5];
                            else chara.bmgOffset = "";
                            characters.Add(chara_parameters[0], chara);
                        }
                        catch { Debug.WriteLine("Failed to parse character: " + line); }
                    }
                    line = read.ReadLine();
                    if (line == null) break;
                    line = line.Split("//", StringSplitOptions.TrimEntries)[0];
                }

                line = read.ReadLine();
                if (line == null) return;
                line = line.Split("//", StringSplitOptions.TrimEntries)[0];

                while (line != null)
                {
                    // Read vehicles until end
                    if (!string.IsNullOrEmpty(line))
                    {
                        try
                        {
                            string[] vehicle_parameters = line.Split("|", StringSplitOptions.TrimEntries);
                            MKW_Vehicle vehicle = new MKW_Vehicle();
                            vehicle.abbrev = vehicle_parameters[1];
                            vehicle.type = vehicle_parameters[2];
                            vehicle.size = vehicle_parameters[3];
                            if (vehicle_parameters[4] == "UniqueMenuTextures") vehicle.usesUniqueMenuTextures = true;
                            else vehicle.usesUniqueMenuTextures = false;
                            vehicles.Add(vehicle_parameters[0], vehicle);
                            if (!vehicleTypes.Contains(vehicle.type)) vehicleTypes.Add(vehicle.type);
                        }
                        catch { Debug.WriteLine("Failed to parse vehicle: " + line); }
                    }
                    line = read.ReadLine();
                    if (line == null) break;
                    line = line.Split("//", StringSplitOptions.TrimEntries)[0];
                }
            }
            else
            {
                MessageBox.Show("Failed to locate MKW_Parameters.txt");
            }
        }
        private void UpdateSamplingType()
        {
            complexSampling = Properties.Settings.Default.complexSampling;
            simpleSamplingToolStripMenuItem.Checked = !complexSampling;
            simpleSamplingToolStripMenuItem.Enabled = complexSampling;
            complexSamplingToolStripMenuItem.Checked = complexSampling;
            complexSamplingToolStripMenuItem.Enabled = !complexSampling;
        }
        private List<string> AvailableVehiclePaths(string sizeType, string charaAbbrev, string modelType)
        {
            List<string> pathList = new List<string>();

            foreach (string key in vehicles.Keys)
            {
                if (vehicles[key].size != sizeType) continue;
                if (vehicles[key].type != modelType) continue;
                string possiblePath = mkwFilePath + $"\\Race\\Kart\\{vehicles[key].abbrev}-{charaAbbrev}.szs";
                if (File.Exists(possiblePath))
                {
                    pathList.Add(possiblePath);
                }
                else Debug.WriteLine($"Failed to find file: {possiblePath}");
            }
            if (pathList.Count == 0) pathList.Add("");
            return pathList;
        }
        private string SampleCharacterName(string path)
        {
            string charName = Path.GetFileName(path);
            if (charName.EndsWith(".sample.szs"))
                charName = charName.Substring(0, charName.Length - 11);
            if (!characters.ContainsKey(charName.Replace("-", " ")) && !characters.ContainsKey(charName))
            {
                MessageBox.Show
                    ($"'{charName}' wasn't a valid character name. " + Environment.NewLine +
                    Environment.NewLine +
                    $"When generating files from multiple samples, " + Environment.NewLine +
                    $"make sure the names of sample files match character names in 'MKW_Parameters.txt'." + Environment.NewLine +
                    Environment.NewLine +
                    "I.E. 'Mario.sample.szs', 'Funky-Kong.sample.szs', etc.");
                progressLabel.Text = "";
                globalProgress.Value = 0;
                return null;
            }
            else return charName.Replace("-", " ");
        }
        static void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            DirectoryInfo[] dirs = dir.GetDirectories();
            Directory.CreateDirectory(destinationDir);
            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath);
            }

            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectory(subDir.FullName, newDestinationDir, true);
                }
            }
        }
        public ProcessStartInfo extractFile(string sourcePath, string arg)
        {
            ProcessStartInfo processStartInfo = new ProcessStartInfo()
            {
                FileName = $"\"{AppDomain.CurrentDomain.BaseDirectory}\\wszst.exe\"",
                Arguments = $" EXTRACT {arg}\"{sourcePath}\"",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
            };
            return processStartInfo;
        }
        public ProcessStartInfo createFile(string sourcePath, string arg)
        {
            ProcessStartInfo processStartInfo = new ProcessStartInfo()
            {
                FileName = $"\"{AppDomain.CurrentDomain.BaseDirectory}\\wszst.exe\"",
                Arguments = $" CREATE {arg}\"{sourcePath}\"",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
            };
            return processStartInfo;
        }
        public ProcessStartInfo extractBMG(string sourcePath, string arg)
        {
            ProcessStartInfo processStartInfo = new ProcessStartInfo()
            {
                FileName = $"\"{AppDomain.CurrentDomain.BaseDirectory}\\wbmgt.exe\"",
                Arguments = $" DECODE {arg}\"{sourcePath}\"",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
            };
            return processStartInfo;
        }
        public ProcessStartInfo createBMG(string sourcePath, string arg)
        {
            ProcessStartInfo processStartInfo = new ProcessStartInfo()
            {
                FileName = $"\"{AppDomain.CurrentDomain.BaseDirectory}\\wbmgt.exe\"",
                Arguments = $" ENCODE {arg}\"{sourcePath}\"",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
            };
            return processStartInfo;
        }
        private void OverwriteName(string bmgPath, string newName, MKW_Character target)
        {
            if (String.IsNullOrEmpty(target.bmgOffset)) return;
            string[] bmgLines = File.ReadAllLines(bmgPath);
            for (int i = 0; i < bmgLines.Length; i++)
            {
                if (bmgLines[i].StartsWith($"  {target.bmgOffset}\t= "))
                {
                    bmgLines[i] = $"  {target.bmgOffset}\t= " + newName;
                }
            }
            File.WriteAllLines(bmgPath, bmgLines);
        }

        public struct MKW_Character
        {
            public string abbrev;
            public string[] iconNames;
            public string size;
            public bool isComplex;
            public string bmgOffset;
        }
        public struct MKW_Vehicle
        {
            public string abbrev;
            public string type;
            public string size;
            public bool usesUniqueMenuTextures;
        }
    }
}