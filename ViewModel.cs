using BladderMin;
using PropertyChanged;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

[assembly: ESAPIScript(IsWriteable = true)]

namespace VMS.TPS
{
    [AddINotifyPropertyChangedInterface]
    public class ViewModel : ObservableObject
    {

        //Initialize some variables to hold the Eclipse related data that will be bound to the UI.
        public string StructureSetId { get; set; }
        public string PlanId { get; set; }
        public ObservableCollection<string> PlanSumList { get; private set; } = new ObservableCollection<string>(); //{"DesignPlan1", "DesignPlan2" };
        public ObservableCollection<string> StructureList { get; private set; } = new ObservableCollection<string>(); //{ "Design1", "Design2", "Design3" };
        public string SelectedBladderContour { get; set; } = "Bladder";
        public string SelectedPlanSum { get; set; }
        public List<Protocol> ProtocolList { get; private set; } = new List<Protocol>();
        public List<string> ProtocolConstraintsList { get; private set; } = new List<string>() { "DesignConstraint1", "DesignConstraint2", "DesignConstraint3" };
        public List<string> ConstraintValuesList { get; private set; } = new List<string>() { "DesignConstraintVal","DesignConstraintVal2", "DesignConstraintVal3"};

        private bool isNodesSelected;
        public bool IsNodesSelected  //Allows user to select whether nodes/pelvis is being treated.
        {
            get
            {
                return isNodesSelected;
            }
            set
            {
                if (isNodesSelected == value)
                    return;

                isNodesSelected = value;

                if (SelectedProtocol != null)
                {
                    SelectedProtocol.SetNodesSelected(isNodesSelected);
                    ProtocolConstraintsList = SelectedProtocol.ProtocolConstraints.Select(x => x.Name).ToList(); // Kludge - NC <-- Ask Nick?
                }
            }
        }
        private Protocol _selectedProtocol;
        public Protocol SelectedProtocol
        {
            get
            {
                return _selectedProtocol;
            }
            set
            {
                _selectedProtocol = value;
                ProtocolConstraintsList = SelectedProtocol.ProtocolConstraints.Select(x => x.Name).ToList();
                RaisePropertyChangedEvent(nameof(PlanSumSelectionVisibility));
                RaisePropertyChangedEvent(nameof(NodalSelectionVisibility));
            }
        }

        //Visibility components of GUI to hide or make visible certain components dependent on user selections
        public Visibility NodalSelectionVisibility
        {
            get
            {
                if (SelectedProtocol != null)
                {
                    if (SelectedProtocol.isNodesTreatable)
                        return Visibility.Visible;
                    else
                        return Visibility.Collapsed;
                }
                else
                    return Visibility.Visible;
            }
        }
        public Visibility PlanSumSelectionVisibility
        {
            get
            {
                if (SelectedProtocol != null)
                {
                    if (SelectedProtocol.isMultiPhase)
                        return Visibility.Visible;
                    else
                        return Visibility.Collapsed;
                }
                else
                    return Visibility.Collapsed;
            }
        }

        //Variables to store script results to the GUI
        public string BlaMinVol { get; set; }
        public string SupMargin { get; set; }
        public string AntMargin { get; set; }

        //Variables for UI related bindings
        public string StatusMessage { get; set; } //= "Design Time";
        public bool ScriptWorking { get; set; } = false;
        public bool ButtonEnabled { get; set; } = true;

        public SolidColorBrush StatusColour { get; set; }
        public SolidColorBrush WarningColour = new SolidColorBrush(Colors.Goldenrod);
        public SolidColorBrush FailColour = new SolidColorBrush(Colors.Tomato);
        public SolidColorBrush ScriptDoneColour = new SolidColorBrush(Colors.PaleGreen);

        public ViewModel()
        {

        }
        public EsapiWorker ew = null;

        //Starts the EsapiWorker.
        public ViewModel(EsapiWorker _ew = null)
        {
            ew = _ew;
            Initialize();
        }

        //Initializes the thread that runs from Eclipse.
        private async void Initialize()
        {
            try
            {
                string currentStructureSetId = "";
                string currentPlanId = "";
                List<string> planSums = new List<string>();
                List<string> structures = new List<string>();
                ProtocolConstraintsList.Clear(); // clear design time parameters;
                ConstraintValuesList.Clear(); // clear design time parameters;
                ObservableCollection<string> structureList = new ObservableCollection<string>();
                ObservableCollection<string> planSumList = new ObservableCollection<string>();
                var libraryBuilder = new ProtocolLibraryBuilder();
                var protocolOptions = libraryBuilder.Library;
                foreach (var name in protocolOptions)
                {
                    ProtocolList.Add(new Protocol(name.Item1, IsNodesSelected));
                }

                bool Done = await Task.Run(() => ew.AsyncRun((p, pl) =>
                {
                    p.BeginModifications();
                    //Get basic patient information and initialize drop down menu variables.
                    currentStructureSetId = pl.StructureSet.Id;
                    currentPlanId = pl.Id;
                    planSums = pl.Course.PlanSums.Select(x => x.Id).ToList();
                    structures = pl.StructureSet.Structures.Select(x => x.Id).ToList();
                    foreach (string structureId in structures)
                    {
                        structureList.Add(structureId);
                    }
                    foreach (string planSumId in planSums)
                    {
                        planSumList.Add(planSumId);
                    }

                }
                ));
                if (Done)
                {
                    StructureSetId = currentStructureSetId;
                    PlanSumList = planSumList;
                    PlanId = currentPlanId;
                    StructureList = structureList;
                    ScriptWorking = false;
                }
            }
            catch (Exception ex)
            {
                SeriLog.AddFatal(string.Format("{0}\r\n{1}\r\n{2}", ex.Message, ex.InnerException, ex.StackTrace));
                MessageBox.Show(string.Format("{0}\r\n{1}\r\n{2}", ex.Message, ex.InnerException, ex.StackTrace));
            }
        }

        public ICommand StartCommand
        {
            get
            {
                return new DelegateCommand(Start);
            }
        }

        public async void Start(object param = null)
        {
            SeriLog.AddInfo("BladderMin application started.");
            ButtonEnabled = false;
            ScriptWorking = true;

            // Clear status message
            StatusMessage = "Running...";
            StatusColour = new SolidColorBrush(Colors.Transparent);

            var bladderMinModel = new BladderminModel(ew); 
            
            var results = await bladderMinModel.CreateBladderMinStructure(SelectedProtocol, SelectedBladderContour, SelectedPlanSum);
            
            if (!results.Success)
            {
                StatusMessage = results.Message;
                StatusColour = FailColour;
                ButtonEnabled = true;
                ScriptWorking = false;
                SeriLog.AddError("Bladdermin did not complete successfully. Please create structure manually.");
                return;
            }
            else
            {
                ConstraintValuesList = results.ProtocolResult.ConstraintResults.Select(x=>x.Volume.ToString("0.#")).ToList();
                BlaMinVol = results.ProtocolResult.BladderMinVolume.ToString("0.##");
                SupMargin = results.MarginResult.TotalSupMargin.ToString("0.#");
                AntMargin = results.MarginResult.TotalAntMargin.ToString("0.#");
            }

            //Closing statements
            ScriptWorking = false;
            StatusMessage = string.Format("Bladdermin structure created.");
            StatusColour = ScriptDoneColour;
            ButtonEnabled = false;
            SeriLog.AddInfo("BladderMin application finished. Bladdermin structure created successfully.");

        }
    }
}
