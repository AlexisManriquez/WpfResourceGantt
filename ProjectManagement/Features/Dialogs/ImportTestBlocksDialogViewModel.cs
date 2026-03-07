using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices; // Needed for COM cleanup
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WpfResourceGantt.ProjectManagement.Models;
// Alias for Excel Interop to avoid naming conflicts
#if ENABLE_MSPROJECT
using MSProject = Microsoft.Office.Interop.MSProject;
using Excel = Microsoft.Office.Interop.Excel;
#endif

namespace WpfResourceGantt.ProjectManagement.Features.Dialogs
{
    public class ImportTestBlocksDialogViewModel : ViewModelBase
    {
        private readonly DataService _dataService;

        public ObservableCollection<WorkBreakdownItem> AvailableGates { get; private set; }

        private WorkBreakdownItem _selectedGate;
        public WorkBreakdownItem SelectedGate
        {
            get => _selectedGate;
            set
            {
                _selectedGate = value;
                OnPropertyChanged();
                (ImportCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        private string _selectedFilePath;
        public string SelectedFilePath
        {
            get => _selectedFilePath;
            set
            {
                _selectedFilePath = value;
                OnPropertyChanged();
                (ImportCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public ICommand BrowseCommand { get; }
        public ICommand ImportCommand { get; }
        public ICommand CancelCommand { get; }

        public event Action<bool> OnCloseRequest;

        public ImportTestBlocksDialogViewModel(DataService dataService, WorkBreakdownItem contextRoot = null)
        {
            _dataService = dataService;
            LoadGates(contextRoot);

            BrowseCommand = new RelayCommand(BrowseFile);
            ImportCommand = new RelayCommand(async () => await ImportAsync(), CanImport);
            CancelCommand = new RelayCommand(() => OnCloseRequest?.Invoke(false));
        }

        private void LoadGates(WorkBreakdownItem contextRoot)
        {
            AvailableGates = new ObservableCollection<WorkBreakdownItem>();

            if (contextRoot != null)
            {
                // If we have a context (e.g. from Gate Progress View), 
                // start recursion from that specific Sub-Project
                AvailableGates.Add(contextRoot);
                RecurseFindGates(contextRoot.Children);
            }
            else if (_dataService.AllSystems != null)
            {
                // Fallback to global search
                foreach (var sys in _dataService.AllSystems)
                {
                    RecurseFindGates(sys.Children);
                }
            }

            // Auto-select if there is only one option or a context root was provided
            if (AvailableGates.Count > 0)
                SelectedGate = AvailableGates[0];
        }

        private void RecurseFindGates(List<WorkBreakdownItem> items)
        {
            if (items == null) return;
            foreach (var item in items)
            {
                if (item.Level >= 2)
                {
                    AvailableGates.Add(item);
                }
                RecurseFindGates(item.Children);
            }
        }

        private void BrowseFile()
        {
            var openFileDialog = new OpenFileDialog
            {
                // UPDATED FILTER: Allows both CSV and XLSX
                Filter = "Data Files (*.csv;*.xlsx)|*.csv;*.xlsx|All Files (*.*)|*.*",
                Title = "Select Test Block Import File"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                SelectedFilePath = openFileDialog.FileName;
            }
        }

        private bool CanImport()
        {
            return SelectedGate != null && !string.IsNullOrEmpty(SelectedFilePath) && File.Exists(SelectedFilePath);
        }

        private async Task ImportAsync()
        {
            string tempCsvPath = null;
            bool isTempFile = false;

            try
            {
                string extension = Path.GetExtension(SelectedFilePath).ToLower();
                string pathProcessing = SelectedFilePath;

                // 1. CONVERSION LOGIC
                if (extension == ".xlsx" || extension == ".xls")
                {
                    // Convert to a temporary CSV
                    tempCsvPath = ConvertExcelToCsv(SelectedFilePath);
                    pathProcessing = tempCsvPath;
                    isTempFile = true;
                }

                // 2. PARSING LOGIC (Same as before, now working on a CSV path)
                var newBlocks = await Task.Run(() => ParseCsv(pathProcessing));

                if (newBlocks.Count == 0)
                {
                    MessageBox.Show("No valid blocks found in file.", "Import Warning");
                    return;
                }

                int startSequence = SelectedGate.ProgressBlocks.Count;

                foreach (var block in newBlocks)
                {
                    block.Sequence = startSequence++;
                    SelectedGate.ProgressBlocks.Add(block);
                }

                SelectedGate.RecalculateRollup();
                await _dataService.SaveDataAsync();

                MessageBox.Show($"Successfully imported {newBlocks.Count} Test Blocks.", "Success");
                OnCloseRequest?.Invoke(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error importing file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // 3. CLEANUP
                if (isTempFile && !string.IsNullOrEmpty(tempCsvPath) && File.Exists(tempCsvPath))
                {
                    try { File.Delete(tempCsvPath); } catch { /* Ignore cleanup errors */ }
                }
            }
        }

        /// <summary>
        /// Uses Microsoft.Office.Interop.Excel to save the active sheet as a CSV.
        /// Warning: Requires Excel to be installed on the machine.
        /// </summary>
        private string ConvertExcelToCsv(string excelFilePath)
        {
#if ENABLE_MSPROJECT
            Excel.Application excelApp = null;
            Excel.Workbook workbook = null;
            string tempCsv = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");

            try
            {
                excelApp = new Excel.Application
                {
                    Visible = false,
                    DisplayAlerts = false
                };

                workbook = excelApp.Workbooks.Open(excelFilePath);

                // Save the Active Sheet as CSV
                workbook.SaveAs(tempCsv, Excel.XlFileFormat.xlCSV);
            }
            finally
            {
                // Strict COM Cleanup to prevent "Ghost" Excel processes
                if (workbook != null)
                {
                    workbook.Close(false);
                    Marshal.ReleaseComObject(workbook);
                }
                if (excelApp != null)
                {
                    excelApp.Quit();
                    Marshal.ReleaseComObject(excelApp);
                }
            }

            return tempCsv;
#else
            throw new NotSupportedException("MS Project import is disabled in this build configuration. To enable, set <UseMsProject>true</UseMsProject> in the .csproj file.");
#endif
        }

        private List<ProgressBlock> ParseCsv(string path)
        {
            var result = new List<ProgressBlock>();
            var lines = File.ReadAllLines(path);

            // Heuristic to remove header if it matches specific keywords
            var dataLines = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            if (dataLines.Count > 0)
            {
                // Check if first row contains "Test Block" (Case insensitive)
                if (dataLines[0].IndexOf("Test Block", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    dataLines.RemoveAt(0);
                }
            }

            ProgressBlock currentBlock = null;

            foreach (var line in dataLines)
            {
                // Handle basic CSV split. 
                // Note: If your CSV has commas INSIDE the text, you need a Regex parser.
                // For now, we assume standard "Block,No,Name" format.
                var parts = line.Split(',');

                if (parts.Length < 3) continue;

                string blockName = parts[0].Trim();
                string testNo = parts[1].Trim();
                // Combine remaining parts in case the name had a comma
                string testName = string.Join(",", parts.Skip(2)).Trim();

                if (currentBlock == null || !currentBlock.Name.Equals(blockName, StringComparison.OrdinalIgnoreCase))
                {
                    currentBlock = new ProgressBlock
                    {
                        Id = "PB-" + Guid.NewGuid().ToString("N").Substring(0, 12),
                        Name = blockName,
                        Items = new List<ProgressItem>(),
                        // Sequence will be set in the ImportAsync method above
                    };
                    result.Add(currentBlock);
                }

                currentBlock.Items.Add(new ProgressItem
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = $"{testNo} - {testName}",
                    IsCompleted = false,
                    Sequence = currentBlock.Items.Count // Sorts items within the block
                });
            }

            return result;
        }
    }
}
