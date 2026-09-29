using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace VMS.TPS
{
    public class EsapiWorker 
    {
        private readonly PlanSetup _pl;
        private readonly StructureSet _ss;
        private readonly Patient _p;
        private readonly Application _app;
        private readonly Dispatcher _dispatcher;
        public string UserId { get; private set; }
        
        public EsapiWorker(PlanSetup pl, Patient p, Application app)
        {
            if (app != null)
                _app = app;
            _pl = pl;
            _ss = pl.StructureSet;
            _p = p;
            _dispatcher = Dispatcher.CurrentDispatcher;
        }
        public EsapiWorker(PlanSetup pl, Patient p, string userId)
        {
            _pl = pl;
            _ss = pl.StructureSet;
            _p = p;
            UserId = userId;
            _dispatcher = Dispatcher.CurrentDispatcher;
        }
        public EsapiWorker(StructureSet ss, Patient p)
        {
            _ss = ss;
            _p = p;
            _dispatcher = Dispatcher.CurrentDispatcher;
        }
        public EsapiWorker(StructureSet ss, Patient p, Application app)
        {
            if (app != null)
                _app = app;
            _ss = ss;
            _p = p;
            _dispatcher = Dispatcher.CurrentDispatcher;
        }
        public void Run(Action<PlanSetup> a)
        {
            _dispatcher.BeginInvoke(a, _pl);
        }
        public void Run(Action<Patient, PlanSetup> a)
        {
            _dispatcher.BeginInvoke(a, _p, _pl);
        }

        public bool RunStructure(Action<StructureSet> a)
        {
            _dispatcher.Invoke(a, _ss);
            return true;
        }

        public async Task<bool> AsyncRun(Action<Patient, PlanSetup> a)
        {
            await _dispatcher.BeginInvoke(a, _p, _pl);
            return true;
        }

        public async Task<bool> AsyncRun(Action<Patient, PlanSetup, Application> a)
        {
            await _dispatcher.BeginInvoke(a, _p, _pl, _app);
            return true;
        }

        public delegate void D(Patient p, StructureSet s);
        public async Task<bool> AsyncRunStructureContext(Action<Patient, StructureSet> a)
        {
            await _dispatcher.BeginInvoke(a, _p, _ss);
            //var D = new SendOrPostCallback(_ => a(_p, _ss));
            //_context.Send(D, null);
            return true;
        }

        public async Task<bool> AsyncAppRun(Action<Application> a)
        {
            await _dispatcher.BeginInvoke(a, _app);
            return true;
        }

   }
}
