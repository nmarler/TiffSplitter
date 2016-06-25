using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace TiffSplitter
{
    internal partial class Form1 : Form
    {

        internal Form1()
        {
            InitializeComponent();

            // error check
            if( null == txtFolder )
                return;

            // fill in path to this executable
            txtFolder.Text = Path.GetDirectoryName( Application.ExecutablePath );

            // move carat to end of text box
            txtFolder.Select(txtFolder.TextLength,0);
        }


        private void btnBrowse_Click(
            object sender,
            System.EventArgs e )
        {
            // error check
            if( null == txtFolder )
                return;

            // create folder browser dialog
            FolderBrowserDialog lobjDialog = new FolderBrowserDialog
            {
                SelectedPath = txtFolder.Text
            };

            // user interaction
            DialogResult dlgResult = lobjDialog.ShowDialog();
            if( dlgResult == DialogResult.OK )
            {
                // Get the path for the selected folder
                txtFolder.Text = lobjDialog.SelectedPath;
            }
        }


        private void txtFolder_TextChanged( 
            object sender, 
            System.EventArgs e )
        {
            if( null != btnSplit )
                btnSplit.Enabled = !string.IsNullOrEmpty( txtFolder?.Text );
        }


        private void btnSplit_Click( 
            object sender, 
            System.EventArgs e )
        {
            // UI
            txtFolder.Enabled = false;
            btnBrowse.Enabled = false;
            btnSplit.Enabled = false;
            Application.DoEvents();

            // error check
            if( string.IsNullOrEmpty( txtFolder?.Text ) ) {
                DisplayError( "Must specify folder to analyze" );
                goto Done;
            }

            // error check
            if( !Directory.Exists( txtFolder.Text ) ) {
                DisplayError( "Must specify a folder that already exists" );
                goto Done;
            }

            // process files in the directory
            int splitCount = Directory.EnumerateFiles( txtFolder.Text ).Count( Split );

            // results to user
            if( splitCount == 0 )
                MessageBox.Show( "No files were split", "Result", MessageBoxButtons.OK, MessageBoxIcon.Warning );
            else
                MessageBox.Show( $"{splitCount} file{( splitCount == 1 ? " was" : "s were" )} split", "Result", MessageBoxButtons.OK, MessageBoxIcon.Information );

            // done
        Done:
            txtFolder.Enabled = true;
            btnBrowse.Enabled = true;
            btnSplit.Enabled = true;
            Application.DoEvents();
        }


        private bool Split(
            string file )
        {
            // verify parameter
            if( string.IsNullOrEmpty( file ) )
                return false;

            UpdateStatus( $"Processing {Path.GetFileName( file )}..." );

            // verify parameter represents a file
            if( !File.Exists( file ) )
            {
                UpdateStatus( $"Processing {Path.GetFileName( file )} Failed (Bad Param)." );
                return false;
            }

            // verify parameter is a tiff file (well, at least that it has proper extension)
            if( !file.ToLower().EndsWith( ".tif" ) && !file.ToLower().EndsWith( ".tiff" ) )
            {
                UpdateStatus( $"Ignore {Path.GetFileName( file )}  - not a TIFF." );
                return false;
            }

            // create an image object to work with
            using( Image tiffImage = Image.FromFile( file ) )
            {
                // get number of pages
                int numPages =
                    tiffImage.GetFrameCount( new FrameDimension( tiffImage.FrameDimensionsList[0] ) );

                // is there anything to Split?
                if( 2 > numPages )
                {
                    UpdateStatus( $"Ignore {Path.GetFileName( file )} - does not contain multiple images." );
                    return false;
                }

                // get tiff codec info
                ImageCodecInfo tiffCodecInfo =
                    ImageCodecInfo.GetImageEncoders().FirstOrDefault( t => t?.MimeType == "image/tiff" );

                // error check
                if( null == tiffCodecInfo ) {
                    string s = "Internal Error - could not obtain TIFF codec";
                    UpdateStatus( $"Processing {Path.GetFileName( file )} failed - {s}." );
                    DisplayError( s );
                    return false;
                }

                // time to Split
                foreach( var guid in tiffImage.FrameDimensionsList ) 
                {
                    UpdateStatus( $"Splitting {Path.GetFileName( file )}..." );

                    for( int index = 0; index < numPages; index++ )
                    {
                        UpdateStatus( $"Splitting {Path.GetFileName( file )} ({index})..." );

                        FrameDimension currentFrame = new FrameDimension( guid );
                        tiffImage.SelectActiveFrame( currentFrame, index );
                        tiffImage.Save( MakeIndexedFileName(file,index), tiffCodecInfo, null );
                    }
                }
            }

            // move the original file to subfolder
            UpdateStatus( $"Moving {Path.GetFileName( file )}..." );
            File.Move(file, MakeSubfolderFileName(file) );

            // success
            UpdateStatus( $"Successfully processed {Path.GetFileName( file )}" );
            return true;
        }


        private static void DisplayError(
            string errMessage )
        {
            MessageBox.Show( errMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error );
        }


        private void UpdateStatus(string msg)
        {
            // error check
            if( null == toolStripStatusLabel )
                return;

            // update
            toolStripStatusLabel.Text = msg;
            Application.DoEvents();
        }


        /// <summary>
        /// Insert index into the provided filename (insert just prior to the file extension). Guaranteed
        /// to be unique.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        private static string MakeIndexedFileName(
            string fileName,
            int index )
        {
            string newPath;

            bool retryFlag = false;
            int retryCount = 1;

            // make unique
            while( true )
            {
                // generate new name (add counter if we are retry-ing)
                string newName =
                    !retryFlag
                    ? $"{Path.GetFileNameWithoutExtension( fileName )}.{index}{Path.GetExtension( fileName )}"
                    : $"{Path.GetFileNameWithoutExtension( fileName )}.{index} ({retryCount++}){Path.GetExtension( fileName )}";

                newPath =
                    Path.Combine( Path.GetDirectoryName( fileName ), newName );

                // unique?
                if( !File.Exists( newPath ) )
                    break;

                // try again
                retryFlag = true;
            }

            // done
            return newPath;
        }


        /// <summary>
        /// Insert subfolder named "Originals" just prior to the filename. Create the subfolder
        /// if it doesn't already exist. File name guaranteed to be unique.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private static string MakeSubfolderFileName(
            string fileName )
        {
            // create new path by inserting subfolder
            string newPath = Path.Combine( Path.GetDirectoryName( fileName ), "Multi-Image TIFF Originals" );

            // create if doesn't exist
            if( !Directory.Exists( newPath ) )
                Directory.CreateDirectory( newPath );

            // add the filename back on
            string newName = Path.Combine( newPath, Path.GetFileName( fileName ) );

            // verify unique, if not rename file
            int retryCount = 1;
            while( true )
            {
                // unique?
                if( !File.Exists( newName ) )
                    break;

                // add counter and try again
                string s =
                    $"{Path.GetFileNameWithoutExtension( fileName )} ({retryCount++}){Path.GetExtension( fileName )}";
                newName = Path.Combine( newPath, s );
            }

            // done
            return newName;
        }
    }
}
