using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TitaniumAS.Opc.Client.Da;
using TitaniumAS.Opc.Client.Da.Browsing;

namespace TechParamsCalc.OPC
{
    interface IOpcClient
    {
        string ParentNodeDescriptor { get; } //Parent OPC Node
        OpcDaServer OpcServer { get; }       //Server OPC
        int OpcGroupsCount { get; set; }

        //Reads Data from OPC. Fills the Nodelist from PLC
        IEnumerable<OpcDaBrowseElement> ReadDataToNodeList(string _subStringPattern);

        //Method writes data to "VAL_CALC" field of Capacity (Density) object and than - to OPC server  
        void WriteMultiplyItems(OpcDaGroup dataGroup, object[] values);
    }
}
