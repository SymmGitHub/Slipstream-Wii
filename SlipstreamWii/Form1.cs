using System.Diagnostics;

namespace SlipstreamWii
{
    public partial class Form1 : Form
    {
        public bool loading;
        public bool complexSampling;
        public string mkwFilePath;
        public Process currentProcess;
        public Dictionary<string, MKW_Character> characters = new Dictionary<string, MKW_Character>();
        public Dictionary<string, MKW_Vehicle> vehicles = new Dictionary<string, MKW_Vehicle>();
        public List<string> vehicleTypes = new List<string>();
        public StreamReader read;
        public Form1()
        {
            InitializeComponent();
            currentProcess = new Process();
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            loading = true;
            progressLabel.Text = "";
            ParseConfig();
            read.Close();
            read.Dispose();
            UpdateSamplingType();
            targetCharBox.Items.AddRange(characters.Keys.ToArray());
            if (targetCharBox.Items.Count > 0) targetCharBox.SelectedIndex = 0;
            RefreshVehicleGenerationList();
            loading = false;
        }
        private async void openMKWFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show($"Please select the 'files' folder inside{Environment.NewLine}an extracted disc of Mario Kart Wii");
            FolderBrowserDialog open = new FolderBrowserDialog();
            if (open.ShowDialog() == DialogResult.OK)
            {
                mkwFilePath = open.SelectedPath;
                SaveSettingsToConfig();
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
                TaskCMD(cmdType.ExtractFile, open.FileName, "", false);
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
                TaskCMD(cmdType.CreateFile, open.SelectedPath, modifier, false);
            }
        }
        private void decodeBmgToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog open = new OpenFileDialog();
            open.Title = "Extract a (.bmg) file";
            open.Filter = "MKW Message File (*.bmg;)|*.bmg;|All files (*.*)|*.*";
            if (open.ShowDialog() == DialogResult.OK)
            {
                TaskCMD(cmdType.DecodeBMG, open.FileName, "", true);
            }
        }
        private void encodeBmgToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog open = new OpenFileDialog();
            open.Title = "Create a (.bmg) file";
            open.Filter = "MKW Message File (*.txt;)|*.txt;|All files (*.*)|*.*";
            if (open.ShowDialog() == DialogResult.OK)
            {
                TaskCMD(cmdType.EncodeBMG, open.FileName, "", true);
            }
        }
        private void changeSamplingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            complexSampling = !complexSampling;
            SaveSettingsToConfig();
            UpdateSamplingType();
        }
        private void createSampleBtn_Click(object sender, EventArgs e)
        {
            // Check if backup folder is defined, prompt user if not
            if (!Directory.Exists(mkwFilePath) || Directory.GetDirectories(mkwFilePath).Length == 0)
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
            save.FileName = $"{targetCharBox.Text.Replace(" ", "-")}.sample.szs";
            save.Filter = "MKW Sample Model File (*.sample.szs)|*.sample.szs|All files (*.*)|*.*";
            if (save.ShowDialog() == DialogResult.OK)
            {
                SaveSettingsToConfig();
                SaveSample(save.FileName, target, "Kart");
                if (complexCheck) changeSamplingToolStripMenuItem_Click(sender, e);
            }
        }
        private void createFilesBtn_Click(object sender, EventArgs e)
        {
            // Check if files folder is defined, prompt user if not
            if (!Directory.Exists(mkwFilePath) || Directory.GetDirectories(mkwFilePath).Length == 0)
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
                SaveSettingsToConfig();
                CreateFilesFromSample(open.FileNames);
            }
        }
        private async void SaveSample(string path, MKW_Character target, string mainModelType)
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
            Directory.CreateDirectory(folderPath + "\\name");
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
            if (File.Exists(mkwFilePath + $"\\Scene\\Model\\Kart\\{target.abbrev}-allkart.szs"))
            {
                File.Copy(mkwFilePath + $"\\Scene\\Model\\Kart\\{target.abbrev}-allkart.szs", tempPath + "\\allkart.szs");
                await TaskCMD(cmdType.ExtractFile, tempPath + "\\allkart.szs", "", true);
            }
            else Debug.WriteLine($"Failed to find allkart model at: {mkwFilePath + $"\\Scene\\Model\\Kart\\{target.abbrev}-allkart.szs"}");
            if (File.Exists(mkwFilePath + $"\\Scene\\Model\\Kart\\{target.abbrev}-allkart_BT.szs"))
            {
                File.Copy(mkwFilePath + $"\\Scene\\Model\\Kart\\{target.abbrev}-allkart_BT.szs", tempPath + "\\allkart_BT.szs");
                await TaskCMD(cmdType.ExtractFile, tempPath + "\\allkart_BT.szs", "", true);
            }
            else Debug.WriteLine($"Failed to find allkart_BT model at: {mkwFilePath + $"\\Scene\\Model\\Kart\\{target.abbrev}-allkart_BT.szs"}");
            globalProgress.Value++;

            for (int i = 0; i < keysToCheck.Count; i++)
            {
                string a = vehicles[keysToCheck[i]].abbrev;
                if (Directory.Exists(tempPath + "\\vehicle.d")) Directory.Delete(tempPath + "\\vehicle.d", true);

                progressLabel.Text = $"Sampling files from {keysToCheck[i]}...";

                // Copy Vehicle files from mkw Race folder to the temporary folder
                string vehiclePath = mkwFilePath + $"\\Race\\Kart\\{a}-{target.abbrev}.szs";
                if (File.Exists(vehiclePath)) File.Copy(vehiclePath, tempPath + "\\vehicle.szs", true);
                else
                {
                    Debug.WriteLine("Failed to find vehicle: " + vehiclePath);
                    continue;
                }

                // Extract Vehicle from copied file to a folder
                await TaskCMD(cmdType.ExtractFile, tempPath + "\\vehicle.szs", "", true);

                // Save the path of the extracted kart folder
                string k = tempPath + "\\vehicle.d";

                if (Directory.Exists(k))
                {
                    string vehicleModelPath = k + "\\kart_model.brres";
                    if (File.Exists(vehicleModelPath))
                    {
                        string mod = "";
                        if (a.EndsWith("blue") || a.EndsWith("red")) mod = "_BT";
                        string allkartPath = tempPath + $"\\allkart{mod}.d\\{a}.brres";

                        if (File.Exists(tempPath + $"\\allkart{mod}.d\\{a}.brres"))
                        {
                            // If a menu version of the model exists in allkart, extract it's model AND this vehicle, then combine them.
                            await TaskCMD(cmdType.ExtractFile, vehicleModelPath, "", true);
                            await TaskCMD(cmdType.ExtractFile, allkartPath, "", true);
                            Directory.Move(allkartPath + ".d\\3DModels(NW4R)", vehicleModelPath + ".d\\Menu_3DModels(NW4R)");
                            if (vehicles[keysToCheck[i]].usesUniqueMenuTextures)
                            {
                                Directory.Move(allkartPath + ".d\\Textures(NW4R)", vehicleModelPath + ".d\\Menu_Textures(NW4R)");
                            }
                            Directory.Delete(allkartPath + ".d", true);
                            await TaskCMD(cmdType.CreateFile, vehicleModelPath + ".d", "", true);
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
                                await TaskCMD(cmdType.ExtractFile, driverModelPath, "", false);

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
                }
                else Debug.WriteLine($"No extracted vehicle path at: {k}");


                globalProgress.Value++;
            }

            // Get Main Menu Animations
            progressLabel.Text = "Fetching Main Menu Animations...";
            string selectCharAnimsPath = mkwFilePath + "\\Scene\\Model\\Driver.szs";
            string selectKartAnimsPath = mkwFilePath + $"\\Scene\\Model\\Kart\\{target.abbrev}-allkart.szs";
            if (File.Exists(selectCharAnimsPath))
            {
                File.Copy(selectCharAnimsPath, tempPath + "\\SelectChar.szs");
                await TaskCMD(cmdType.ExtractFile, tempPath + "\\SelectChar.szs", "", false);

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
                            await TaskCMD(cmdType.ExtractFile, tempPath + $"\\SelectChar.d\\{target.abbrev}{mod}.brres", "", false);

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
                await TaskCMD(cmdType.ExtractFile, tempPath + "\\SelectKart.szs", "", false);
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
                await TaskCMD(cmdType.ExtractFile, tempPath + "\\Award.szs", "", false);
                string[] complexAwardSampling = new string[2] { "", "3" };
                foreach (string mod in complexAwardSampling)
                {
                    if (File.Exists(tempPath + $"\\Award.d\\{target.abbrev}{mod}.brres"))
                    {
                        Directory.CreateDirectory(folderPath + $"\\award{mod}.brres.d");
                        await TaskCMD(cmdType.ExtractFile, tempPath + $"\\Award.d\\{target.abbrev}{mod}.brres", "", false);
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
                await TaskCMD(cmdType.ExtractFile, tempPath + "\\Icons.szs", "", false);
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
                await TaskCMD(cmdType.ExtractFile, tempPath + "\\IconText.szs", "", false);
                if (File.Exists(tempPath + "\\IconText.d\\message\\Common.bmg"))
                {
                    await TaskCMD(cmdType.DecodeBMG, tempPath + "\\IconText.d\\message\\Common.bmg", "", false);
                    read = new StreamReader(tempPath + "\\IconText.d\\message\\Common.txt");
                    string[] bmgLines = read.ReadToEnd().Split(Environment.NewLine);
                    read.Close();
                    read.Dispose();
                    for (int i = 0; i < bmgLines.Length; i++)
                    {
                        string targetOffset = $"  {target.bmgOffset}\t= ";
                        if (bmgLines[i].StartsWith(targetOffset))
                        {
                            StreamWriter write = new StreamWriter(folderPath + "\\name\\" + bmgLines[i].Substring(targetOffset.Length));
                            write.WriteLine("");
                            write.Close();
                            write.Dispose();
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
                        await TaskCMD(cmdType.CreateFile, dir, "--brres --no-compress ", false);
                    }
                    Directory.Delete(dir, true);
                }
            }
            globalProgress.Value++;

            progressLabel.Text = "Building Sample Model...";
            if (File.Exists(path)) File.Delete(path);
            await TaskCMD(cmdType.CreateFile, folderPath, "", false);

            // If the new file was created, delete old directory
            if (File.Exists(path)) Directory.Delete(folderPath, true);
            globalProgress.Value++;

            // Delete Temp folder when all processing is done
            Directory.Delete(tempPath, true);
            progressLabel.Text = "Done!";
            globalProgress.Value++;
        }

        private async void CreateFilesFromSample(string[] paths)
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
            Directory.CreateDirectory(outputFolder + "\\Race");
            Directory.CreateDirectory(outputFolder + "\\Race\\Kart");
            Directory.CreateDirectory(outputFolder + "\\Demo");
            Directory.CreateDirectory(outputFolder + "\\Scene");
            Directory.CreateDirectory(outputFolder + "\\Scene\\Model");
            Directory.CreateDirectory(outputFolder + "\\Scene\\Model\\Kart");
            Directory.CreateDirectory(outputFolder + "\\Scene\\UI");
            globalProgress.Value++;

            foreach (string iconLocation in allIconLocations)
            {
                string name = iconLocation.Split(", ")[0];
                progressLabel.Text = $"Extracting UI SZS Files {name}(_{targetLanguageBox.Text}).szs...";
                if (File.Exists(mkwFilePath + $"\\Scene\\UI\\{name}.szs"))
                {
                    // Extract UI Textures SZS
                    File.Copy(mkwFilePath + $"\\Scene\\UI\\{name}.szs", outputFolder + $"\\Scene\\UI\\{name}.szs", true);
                    await TaskCMD(cmdType.ExtractFile, outputFolder + $"\\Scene\\UI\\{name}.szs", "", true);
                }
                if (File.Exists(mkwFilePath + $"\\Scene\\UI\\{name}_{targetLanguageBox.Text}.szs"))
                {
                    // Extract UI Language SZS
                    File.Copy(mkwFilePath + $"\\Scene\\UI\\{name}_{targetLanguageBox.Text}.szs", outputFolder + $"\\Scene\\UI\\{name}_{targetLanguageBox.Text}.szs", true);
                    await TaskCMD(cmdType.ExtractFile, outputFolder + $"\\Scene\\UI\\{name}_{targetLanguageBox.Text}.szs", "", true);
                }
                globalProgress.Value++;
            }

            // Get the common.bmg in Race_U.szs
            if (File.Exists(outputFolder + $"\\Scene\\UI\\Race_{targetLanguageBox.Text}.d\\message\\Common.bmg"))
            {
                string bmgPath = AppDomain.CurrentDomain.BaseDirectory + "\\Common";
                File.Copy(outputFolder + $"\\Scene\\UI\\Race_{targetLanguageBox.Text}.d\\message\\Common.bmg", bmgPath + ".bmg", true);
                if (File.Exists(bmgPath + ".txt")) File.Delete(bmgPath + ".txt");
                await TaskCMD(cmdType.DecodeBMG, bmgPath + ".bmg", "", true);
            }

            // Copy and Extract Driver and Award Files
            Process extract = new Process();
            progressLabel.Text = "Extracting Driver.szs...";
            if (File.Exists(mkwFilePath + $"\\Scene\\Model\\Driver.szs"))
            {
                File.Copy(mkwFilePath + $"\\Scene\\Model\\Driver.szs", outputFolder + $"\\Scene\\Model\\Driver.szs", true);
                await TaskCMD(cmdType.ExtractFile, outputFolder + $"\\Scene\\Model\\Driver.szs", "", true);
            }
            globalProgress.Value++;
            progressLabel.Text = "Extracting Award.szs...";
            if (File.Exists(mkwFilePath + $"\\Demo\\Award.szs"))
            {
                File.Copy(mkwFilePath + $"\\Demo\\Award.szs", outputFolder + $"\\Demo\\Award.szs", true);
                await TaskCMD(cmdType.ExtractFile, outputFolder + $"\\Demo\\Award.szs", "", true);
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
                await TaskCMD(cmdType.ExtractFile, tempPath + "\\sample.szs", "", true);

                if (File.Exists(tempPath + "\\sample.d\\driver.brres"))
                {
                    // Extract driver brres
                    await TaskCMD(cmdType.ExtractFile, tempPath + "\\sample.d\\driver.brres", "", true);
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
                        await TaskCMD(cmdType.ExtractFile, tempPath + $"\\anims_{type}.brres", "", true);
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
                    await TaskCMD(cmdType.CreateFile, tempPath + $"\\{type}.brres.d", "--brres --no-compress ", true);
                    Directory.Delete(tempPath + $"\\anims_{type}.brres.d", true);
                    globalProgress.Value++;
                }

                // #############################################################################################
                // ##################################### AWARD CEREMONY SZS ####################################
                // #############################################################################################

                progressLabel.Text = $"Creating new {charName} brres file(s) for Award.szs...";
                string awardPath = outputFolder + "\\Demo\\Award.d";
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
                            await TaskCMD(cmdType.ExtractFile, tempPath + $"\\sample.d\\award{mod}.brres", "", false);
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
                                await TaskCMD(cmdType.CreateFile, tempPath + "\\AwardFromDriver.brres.d", "--brres --no-compress ", true);

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
                                    await TaskCMD(cmdType.ExtractFile, tempPath + $"\\{target.abbrev}{mod}.brres", "", true);
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

                                await TaskCMD(cmdType.CreateFile, tempPath + $"\\{target.abbrev}{mod}.brres.d", "--brres --no-compress ", true);
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
                string driverPath = outputFolder + "\\Scene\\Model\\Driver.d";
                if (Directory.Exists(driverPath))
                {
                    // Extract Sample's Character Select Animations
                    if (File.Exists(tempPath + "\\sample.d\\select_char.brres"))
                    {
                        await TaskCMD(cmdType.ExtractFile, tempPath + "\\sample.d\\select_char.brres", "", false);

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
                        await TaskCMD(cmdType.CreateFile, tempPath + "\\DriverMenu.brres.d", "--brres --no-compress ", false);

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
                    await TaskCMD(cmdType.ExtractFile, tempPath + "\\vehicle.szs", "", true);

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
                        await TaskCMD(cmdType.ExtractFile, sampleVehiclePath, "", true);
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
                        // Build In-Game Vehicle and its Allkart equivalent
                        await TaskCMD(cmdType.CreateFile, tempPath + "\\gameVehicle.brres.d", "--brres --no-compress ", true);
                        File.Move(tempPath + "\\gameVehicle.brres", tempPath + "\\vehicle.d\\kart_model.brres", true);
                        await TaskCMD(cmdType.CreateFile, tempPath + "\\allkartVehicle.brres.d", "--brres --no-compress ", true);
                        if ((keysToCheck[i].EndsWith("Blue") || keysToCheck[i].EndsWith("Red")) && BT_Allowed)
                        {
                            // Move this vehicle into BT allkart folder instead
                            File.Move(tempPath + "\\allkartVehicle.brres", tempPath + $"\\Scene\\Model\\Kart\\{target.abbrev}-allkart_BT.d\\{vehicles[keysToCheck[i]].abbrev}.brres");
                        }
                        else File.Move(tempPath + "\\allkartVehicle.brres", tempPath + $"\\Scene\\Model\\Kart\\{target.abbrev}-allkart.d\\{vehicles[keysToCheck[i]].abbrev}.brres");
                    }
                    else Debug.WriteLine("Failed to find sample vehicle: " + sampleVehiclePath);

                    // Rebuild vehicle and move it to output folder
                    await TaskCMD(cmdType.CreateFile, tempPath + "\\vehicle.d", "", true);
                    File.Copy(tempPath + "\\vehicle.szs", outputFolder + $"\\Race\\Kart\\{vehicles[keysToCheck[i]].abbrev}-{target.abbrev}.szs", true);
                    if (vehicleGeneratorList.GetItemChecked(0))
                    {
                        File.Copy(tempPath + "\\vehicle.szs", outputFolder + $"\\Race\\Kart\\{vehicles[keysToCheck[i]].abbrev}-{target.abbrev}_4.szs", true);
                    }
                    globalProgress.Value++;
                }

                // Build AllKart folders and move them to output folder
                foreach (string mod in allKartModifiers)
                {
                    progressLabel.Text = $"Building {target.abbrev}-allkart{mod}.szs...";
                    await TaskCMD(cmdType.CreateFile, tempPath + $"\\Scene\\Model\\Kart\\{target.abbrev}-allkart{mod}.d", "", true);
                    File.Move(tempPath + $"\\Scene\\Model\\Kart\\{target.abbrev}-allkart{mod}.szs", outputFolder + $"\\Scene\\Model\\Kart\\{target.abbrev}-allkart{mod}.szs", true);
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
                            string timgPath = outputFolder + $"\\Scene\\UI\\{loc[0]}.d\\{folderName}\\timg";
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
            progressLabel.Text = "Repacking driver.szs...";
            if (Directory.Exists(outputFolder + "\\Scene\\Model\\Driver.d"))
            {
                await TaskCMD(cmdType.CreateFile, outputFolder + "\\Scene\\Model\\Driver.d", "", true);
            }
            globalProgress.Value++;

            progressLabel.Text = "Repacking Award.szs...";
            if (Directory.Exists(outputFolder + "\\Demo\\Award.d"))
            {
                await TaskCMD(cmdType.CreateFile, outputFolder + "\\Demo\\Award.d", "", true);
            }
            globalProgress.Value++;

            // Create new BMG to insert into the language szs files
            progressLabel.Text = "Creating Common.bmg...";
            if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\Common.txt"))
            {
                await TaskCMD(cmdType.EncodeBMG, AppDomain.CurrentDomain.BaseDirectory + "\\Common.txt", "", true);
            }

            globalProgress.Value++;
            foreach (string iconLocation in allIconLocations)
            {
                string name = iconLocation.Split(", ")[0];
                progressLabel.Text = $"Repacking UI SZS files: {name}(_{targetLanguageBox.Text}).szs...";
                if (Directory.Exists(outputFolder + $"\\Scene\\UI\\{name}.d"))
                {
                    await TaskCMD(cmdType.CreateFile, outputFolder + $"\\Scene\\UI\\{name}.d", "", true);
                }
                if (Directory.Exists(outputFolder + $"\\Scene\\UI\\{name}_{targetLanguageBox.Text}.d"))
                {
                    if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\Common.bmg") &&
                        File.Exists(outputFolder + $"\\Scene\\UI\\{name}_{targetLanguageBox.Text}.d\\message\\Common.bmg"))
                    {
                        File.Copy(AppDomain.CurrentDomain.BaseDirectory + "\\Common.bmg", outputFolder + $"\\Scene\\UI\\{name}_{targetLanguageBox.Text}.d\\message\\Common.bmg", true);
                    }
                    await TaskCMD(cmdType.CreateFile, outputFolder + $"\\Scene\\UI\\{name}_{targetLanguageBox.Text}.d", "", true);
                }
                globalProgress.Value++;
            }

            // Clear Temp Folder
            if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\Common.bmg"))
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
            vehicleGeneratorList.Items.Add("Multiplayer Vehicle Duplicate Models", true);
            vehicleGeneratorList.Items.Add("Colored Standard Vehicle Models", true);
            foreach (string vehicleType in vehicleTypes)
            {
                vehicleGeneratorList.Items.Add($"{vehicleType}s", true);
            }
        }
        private void ParseConfig()
        {
            string paramPath = AppDomain.CurrentDomain.BaseDirectory + "\\Config.txt";

            if (File.Exists(paramPath))
            {
                vehicleTypes = new List<string>();
                characters = new Dictionary<string, MKW_Character>();
                vehicles = new Dictionary<string, MKW_Vehicle>();
                read = new StreamReader(paramPath);
                string line = read.ReadLine();
                if (line == null) return;
                line = line.Split("//", StringSplitOptions.TrimEntries)[0];

                while (line != null && !line.Contains(" SETTINGS "))
                {
                    // Read until reaching SETTINGS
                    line = read.ReadLine();
                    if (line == null) return;
                    line = line.Split("//", StringSplitOptions.TrimEntries)[0];
                }

                line = read.ReadLine();
                if (line == null) return;
                line = line.Split("//", StringSplitOptions.TrimEntries)[0];

                while (line != null && !line.Contains(" CHARACTERS "))
                {
                    // Read Setting until reaching CHARACTERS
                    if (!string.IsNullOrEmpty(line))
                    {
                        try
                        {
                            string type = line.Split(":", 2, StringSplitOptions.TrimEntries)[0];
                            string data = line.Split(":", 2, StringSplitOptions.TrimEntries)[1];
                            switch (type)
                            {
                                case "complex-sampling":
                                    complexSampling = bool.Parse(data);
                                    break;
                                case "ui-language":
                                    targetLanguageBox.Text = data;
                                    break;
                                case "path-mkw-files":
                                    mkwFilePath = data;
                                    break;
                            }
                        }
                        catch { Debug.WriteLine("Failed to parse setting: " + line); }
                    }
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
                MessageBox.Show("Failed to locate Config.txt");
            }
        }
        private void SaveSettingsToConfig()
        {
            read = new StreamReader(AppDomain.CurrentDomain.BaseDirectory + "\\Config.txt");
            string[] config = read.ReadToEnd().Split(Environment.NewLine, StringSplitOptions.TrimEntries);
            read.Close();
            read.Dispose();
            for (int i = 0; i < config.Length; i++)
            {
                string line = config[i];
                string type = line.Split(":", StringSplitOptions.TrimEntries)[0].ToLower();
                switch (type)
                {
                    case "complex-sampling":
                        config[i] = $"{type}: {complexSampling.ToString()}";
                        break;
                    case "target-language":
                        if (!String.IsNullOrEmpty(targetLanguageBox.Text))
                            config[i] = $"{type}: {targetLanguageBox.Text}";
                        break;
                    case "path-mkw-files":
                        config[i] = $"{type}: {mkwFilePath}";
                        break;
                }
            }
            StreamWriter write = new StreamWriter(AppDomain.CurrentDomain.BaseDirectory + "\\Config.txt");
            for (int i = 0; i < config.Length; i++)
            {
                write.WriteLine(config[i]);
            }
            write.Close();
            write.Dispose();
        }
        private void UpdateSamplingType()
        {
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
                    $"make sure the names of sample files match character names in 'Config.txt'." + Environment.NewLine +
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
        public Task TaskCMD(cmdType type, string sourcePath, string arg, bool deleteSource)
        {
            string toolpath = "";
            string command = "";
            string output = "";
            switch (type)
            {
                case cmdType.ExtractFile:
                    toolpath = "SzsTools\\wszst.exe";
                    command = "EXTRACT";
                    break;
                case cmdType.CreateFile:
                    toolpath = "SzsTools\\wszst.exe";
                    command = "CREATE";
                    break;
                case cmdType.DecodeBMG:
                    toolpath = "SzsTools\\wbmgt.exe";
                    command = "DECODE";
                    break;
                case cmdType.EncodeBMG:
                    toolpath = "SzsTools\\wbmgt.exe";
                    command = "ENCODE";
                    break;
                case cmdType.DumpDisc:
                    toolpath = "SzsTools\\wit.exe";
                    command = "EXTRACT";
                    output = $" {AppDomain.CurrentDomain.BaseDirectory}\\Backup";
                    break;
            }
            currentProcess = new Process();
            currentProcess.StartInfo = new ProcessStartInfo()
            {
                FileName = $"{AppDomain.CurrentDomain.BaseDirectory}\\{toolpath}",
                Arguments = $" {command} {arg}{sourcePath}{output}",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
            };
            currentProcess.Start();
            currentProcess.WaitForExit();
            currentProcess.Dispose();
            if (deleteSource)
            {
                if (File.Exists(sourcePath)) File.Delete(sourcePath);
                else if (Directory.Exists(sourcePath)) Directory.Delete(sourcePath, true);
            }
            return Task.CompletedTask;
        }
        private void OverwriteName(string bmgPath, string newName, MKW_Character target)
        {
            if (String.IsNullOrEmpty(target.bmgOffset)) return;
            read = new StreamReader(bmgPath);
            string[] bmgLines = read.ReadToEnd().Split(Environment.NewLine);
            read.Close();
            read.Dispose();
            for (int i = 0; i < bmgLines.Length; i++)
            {
                if (bmgLines[i].StartsWith($"  {target.bmgOffset}\t= "))
                {
                    bmgLines[i] = $"  {target.bmgOffset}\t= " + newName;
                }
            }
            StreamWriter write = new StreamWriter(bmgPath);
            for (int i = 0; i < bmgLines.Length; i++)
            {
                write.WriteLine(bmgLines[i]);
            }
            write.Close();
            write.Dispose();
        }

        private void vehicleListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string message = $"{mkwFilePath}{Environment.NewLine}";
            foreach (string name in vehicles.Keys)
            {
                MKW_Vehicle v = vehicles[name];
                message += $"{name}: {v.abbrev}, {v.type}, {v.size}, {v.usesUniqueMenuTextures}{Environment.NewLine}";
            }
            MessageBox.Show(message);
        }

        public enum cmdType
        {
            ExtractFile,
            CreateFile,
            DecodeBMG,
            EncodeBMG,
            DumpDisc
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