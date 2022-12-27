namespace BARSReaderGUI
{
    public partial class Form1 : Form
    {
        List<AudioAsset> audioAssets = new List<AudioAsset>();
        public Form1()
        {
            InitializeComponent();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Stream fileStream;
            OpenFileDialog fileDialog = new OpenFileDialog();

            if (fileDialog.ShowDialog() == DialogResult.OK)
            {
                audioAssets.Clear();
                AssetListBox.Items.Clear();
                string inputFile = fileDialog.FileName;

                // Check for compression first.
                using (NativeReader reader = new(new FileStream(inputFile, FileMode.Open)))
                {
                    if (reader.ReadUInt() == 0xFD2FB528)
                    {
                        ZstdUtils zstdUtils = new ZstdUtils();
                        reader.Position -= 4;
                        fileStream = zstdUtils.Decompress(reader.BaseStream); // Stores decompressed data into stream
                    }
                    else
                    {
                        reader.Position -= 4;
                        fileStream = new MemoryStream(reader.BaseStream.ToArray()); // If no compression is found, we store the original file data in the stream.
                    }
                }

                // Read the file stored in the stream.
                using (NativeReader reader = new NativeReader(fileStream))
                {
                    //KeyValuePair<uint, AssetOffsetPair>[] assets;

                    string magic = reader.ReadSizedString(4);
                    if (magic != "BARS")
                    {
                        MessageBox.Show("Not a BARS file.");
                        return;
                    }

                    uint size = reader.ReadUInt();

                    ushort endian = reader.ReadUShort();
                    if (endian != 0xFEFF)
                    {
                        MessageBox.Show("Unsupported endian!");
                        return;
                    }

                    ushort version = reader.ReadUShort();
                    if (version != 0x0102 && version != 0x0101)
                    {
                        MessageBox.Show("This version of BARS is unsupported at this time."); //throw this error on trying to read anything other than v1.1/1.2
                        return;
                    }

                    uint assetcount = reader.ReadUInt();
                    //assets = new KeyValuePair<uint, AssetOffsetPair>[assetcount];

                    // Create audioAssets and tie crcHashes to them.
                    for (int i = 0; i < assetcount; i++)
                    {
                        audioAssets.Add(new AudioAsset());
                        audioAssets[i].crcHash = reader.ReadUInt();
                    }

                    // Pair ATMA/BWAV offsets with asset
                    for (int i = 0; i < assetcount; i++)
                    {
                        audioAssets[i].amtaOffset = reader.ReadUInt();
                        audioAssets[i].assetOffset = reader.ReadUInt();
                    }

                    for (int i = 0; i < assetcount; i++)
                    {
                        audioAssets[i].amtaData = new AMTA();
                        audioAssets[i].amtaData.ReadAMTA(audioAssets[i].amtaOffset, reader);
                        AssetListBox.Items.Add(audioAssets[i].amtaData.assetName);
                    }

                    for (int i = 0; i < audioAssets.Count; i++)
                    {
                        reader.Position = audioAssets[i].assetOffset;
                        if (i != audioAssets.Count - 1)
                            audioAssets[i].assetData = reader.ReadBytes(Convert.ToInt32(audioAssets[i + 1].assetOffset - audioAssets[i].assetOffset));
                        else
                            audioAssets[i].assetData = reader.ReadBytes(Convert.ToInt32(size - audioAssets[i].assetOffset));
                    }

                    // Get names for audioAssets
                    //for (int i = 0; i < assetcount; i++)
                    //{
                    //    reader.Position = audioAssets[i].amtaOffset + 0x24;
                    //    uint unkOffset = reader.ReadUInt();
                    //    reader.Position = audioAssets[i].amtaOffset + unkOffset + 36;
                    //    audioAssets[i].assetName = reader.ReadNullTerminatedString();
                    //    BwavListBox.Items.Add(audioAssets[i].assetName);
                    //}

                    //// Read AMTA data
                    //for (int i = 0; i < assetcount; i++)
                    //{
                    //    audioAssets[i].amtaData = new AMTA();
                    //    reader.Position = audioAssets[i].amtaOffset + 8;
                    //    uint amtaSize = reader.ReadUInt();
                    //    //audioAssets[i].amtaData.data = reader.ReadBytes(Convert.ToInt32(amtaSize));
                    //}


                    this.Text = $"BARSReaderGUI - {fileDialog.SafeFileName} - {assetcount} Assets";
                    MessageBox.Show("Successfully read " + assetcount + " assets.");
                }
            }
        }

        private void AssetListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                extractAudioButton.Enabled = true;
                int index = AssetListBox.SelectedIndex;
                AudioAssetNameLabel.Text = audioAssets[index].amtaData.assetName;
                AudioAssetCrc32HashLabel.Text = audioAssets[index].crcHash.ToString("X");
                AudioAssetAmtaOffsetLabel.Text = audioAssets[index].amtaOffset.ToString("X");
                AudioAssetBwavOffsetLabel.Text = audioAssets[index].assetOffset.ToString("X");
            }
            catch (Exception ex)
            {
                extractAudioButton.Enabled = false;
            }
        }

        private void extractAudioButton_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "BWAV files (*.bwav)|*.bwav"; // Change this later to handle other audio formats
            saveFileDialog.Title = "Extract Audio";
            saveFileDialog.FileName = audioAssets[AssetListBox.SelectedIndex].amtaData.assetName;
            saveFileDialog.RestoreDirectory = true;

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                using var writer = new BinaryWriter(File.OpenWrite(saveFileDialog.FileName));
                writer.Write(audioAssets[AssetListBox.SelectedIndex].assetData);
                MessageBox.Show(audioAssets[AssetListBox.SelectedIndex].amtaData.assetName + " extracted successfully.");
            }
        }
    }
    public class AssetOffsetPair
    {
        public uint amtaoffset;
        public uint bwavoffset;
    }
}