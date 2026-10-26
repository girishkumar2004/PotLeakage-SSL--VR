// GENERATED from PotLeakage_Main.unity (TrainingFramework/Sequence/ListOfSequence + CameraSystem/TransformPoints)
using UnityEngine;

public static class VRSceneData
{
    public struct TaskDef
    {
        public string objectName, sequenceName, taskName, instruction, title, description, snapKind, snapValue;
        public TaskDef(string o, string s, string t, string i, string ti, string d, string sk, string sv)
        { objectName = o; sequenceName = s; taskName = t; instruction = i; title = ti; description = d; snapKind = sk; snapValue = sv; }
    }

    public struct PointDef
    {
        public string name; public Vector3 pos; public Quaternion rot; public Vector3 scale; public bool active;
        public PointDef(string n, Vector3 p, Quaternion r, Vector3 s, bool a) { name = n; pos = p; rot = r; scale = s; active = a; }
    }

    public static readonly TaskDef[] Tasks =
    {
        new TaskDef("TASK_01_WELCOME", "TASK_01_WELCOME", "01 – Welcome", "Welcome to the Vedanta Pot Leakage Training Visualization. This module covers the Emergency Response Procedure for Side Shell Leakage at Upper Location.", "Welcome", "Welcome to the Vedanta Pot Leakage Training Visualization. This module covers the Emergency Response Procedure for Side Shell Leakage at Upper Location.", "T", "Welcome"),
        new TaskDef("TASK_02_NORMAL_POT_OPERATION", "TASK_02_NORMAL_POT_OPERATION", "02 – Normal Pot Operation", "In normal production, the reduction pot operates at stable temperature and voltage parameters.", "Normal Pot Operation", "In normal production, the reduction pot operates at stable temperature and voltage parameters.", "T", "Normal Pot Operation"),
        new TaskDef("TASK_03_LEAKAGE_OCCURRED", "TASK_03_LEAKAGE_OCCURRED", "03 – Leakage Occurred", "Pot leakage has occurred in Pot 4321.", "Pot Leakage Occurred", "Pot leakage has occurred in Pot 4321.", "T", "SST"),
        new TaskDef("TASK_06_EMERGENCY_COMMUNICATION", "TASK_06_EMERGENCY_COMMUNICATION", "06 – Emergency Communication", "Inform the Shift Superintendent and Technical In-charge immediately through the walkie-talkie.", "Emergency Communication", "Inform the Shift Superintendent and Technical In-charge immediately through the walkie-talkie.", "T", "SST"),
        new TaskDef("TASK_TOOL_SELECTION", "TASK_TOOL_SELECTION", "Tool Selection", "Select SST Cooling Connector in order to reduce the intensity of the leakage.", "Tool Selection", "Select SST Cooling Connector in order to reduce the intensity of the leakage.", "T", "ToolSelection"),
        new TaskDef("TASK_4_SST_CONNECTION", "TASK_4_SST_CONNECTION", "04 – SST Connection", "Let us use the SST Cooling Connector in the affected area. Click the SST Cooling Connector in the CollectedToolsPanel container.", "SST Connection", "Let us use the SST Cooling Connector in the affected area. Click the SST Cooling Connector in the CollectedToolsPanel container.", "T", "SST"),
        new TaskDef("TASK_5_AIR_Unlock", "TASK_5_AIR_Unlock", "05 – Air Unlock Valve", "Let us now open the valve to release compressed air in the affected area.", "Air Unlock Valve", "Let us now open the valve to release compressed air in the affected area.", "T", "AirUnlock"),
        new TaskDef("TASK_6_SLIDE_RIGHT_HOOD", "TASK_6_SLIDE_RIGHT_HOOD", "06 – Slide Right Hood (Plane.023)", "Slide the right hood.", "Slide Right Hood", "Slide the right hood.", "", ""),
        new TaskDef("TASK_05_SLIDE_LEFT_HOOD", "TASK_05_SLIDE_LEFT_HOOD", "05 – Slide Left Hood (Plane.058)", "Slide the left hood panel outward.", "Slide Left Hood", "Slide the left hood panel outward.", "T", "Normal Pot "),
        new TaskDef("TASK_CRUST_BATHING", "TASK_CRUST_BATHING", "Crust Bathing", "Let us start Crust Breaking Process by bring PTM crane to the leaked potline", "Crust Breaking", "Let us start Crust Breaking Process by bring PTM crane to the leaked potline", "T", "PTM Crane"),
        new TaskDef("TASK_CONTROL_MACHINE", "TASK_CONTROL_MACHINE", "Pot Control Machine", "Check the Voltage and Beam Level. Make sure both are within the safe operating limits.", "Pot Control Machine", "Check the Voltage and Beam Level. Make sure both are within the safe operating limits.", "T", "PotControlMachine"),
        new TaskDef("TASK_BATH_PIPE_POWDER", "TASK_BATH_PIPE_POWDER", "Bath Powder Flow", "Now let us use bath powder from PTM crane.", "Bath Powder", "Now let us use bath powder from PTM crane.", "N", "SST"),
        new TaskDef("TASK_THROW_FUSED_ALUMINA", "TASK_THROW_FUSED_ALUMINA", "Throw Fused Alumina", "Throw fused alumina to seal the bath leakage area.", "Throw Fused Alumina", "Throw fused alumina to seal the bath leakage area.", "N", "SST"),
        new TaskDef("TASK_LEAKAGE_STOP", "TASK_LEAKAGE_STOP", "Leakage Verification", "After all these processes the leakage must be stopped, so let us check if there is still leakage.", "Leakage Verification", "After all these processes the leakage must be stopped, so let us check if there is still leakage.", "N", "SST"),
        new TaskDef("CLOSE_HOOD", "CLOSE_HOOD", "Close Hood", "Pot leakage is completely stopped. Let us close the hood.", "Close Hood", "Pot leakage is completely stopped. Let us close the hood.", "N", "SST"),
        new TaskDef("TASK_TRAINING_COMPLETED", "TASK_27_TRAINING_COMPLETED", "27 – Training Completed", "Pot Leakage Emergency Response Training Completed.", "Training Complete", "Pot Leakage Emergency Response Training Completed.", "T", "Welcome"),
    };

    public static readonly PointDef[] Points =
    {
        new PointDef("Normal Pot ", new Vector3(-2.88346f,1.65300f,40.16300f), new Quaternion(0.00000f,-1.00000f,0.00000f,0.00000f), new Vector3(1.00000f,1.00000f,1.00000f), true),
        new PointDef("ToolBoxSelection (1)", new Vector3(11.76800f,3.58600f,22.47200f), new Quaternion(0.16765f,0.70050f,-0.15056f,0.67715f), new Vector3(1.00000f,1.00000f,1.00000f), true),
        new PointDef("PotMachine_LookTarget", new Vector3(-8.34500f,2.90300f,-65.65100f), new Quaternion(0.00000f,-0.00549f,0.00000f,0.99998f), new Vector3(1.00000f,1.00000f,1.00000f), true),
        new PointDef("Ideal CBT Temperature", new Vector3(-13.86600f,0.85500f,-66.22500f), new Quaternion(0.00000f,-0.05178f,0.00000f,0.99866f), new Vector3(1.00000f,1.00000f,1.00000f), true),
        new PointDef("SST", new Vector3(-2.88346f,3.04000f,40.40700f), new Quaternion(0.00000f,-0.95324f,0.30222f,0.00000f), new Vector3(1.00000f,1.00000f,1.00000f), true),
        new PointDef("Welcome", new Vector3(5.81000f,1.82000f,35.31000f), new Quaternion(0.03317f,-0.69331f,0.03440f,0.71905f), new Vector3(1.00000f,1.00000f,1.00000f), true),
        new PointDef("SIDEREAKING TOOL (5)", new Vector3(-6.26900f,0.74400f,-229.28700f), new Quaternion(-0.99923f,0.00000f,0.00000f,0.03922f), new Vector3(2.06130f,2.06130f,75.79227f), true),
        new PointDef("VoltageScale", new Vector3(-2.39700f,3.84200f,-230.74800f), new Quaternion(0.00000f,0.00000f,0.00000f,1.00000f), new Vector3(1.00000f,1.00000f,1.00000f), true),
        new PointDef("drop", new Vector3(-2.87700f,0.18600f,38.31000f), new Quaternion(0.00000f,0.00000f,0.00000f,1.00000f), new Vector3(1.00000f,1.00000f,1.00000f), true),
        new PointDef("ToolSelection", new Vector3(12.83000f,3.42000f,30.22000f), new Quaternion(0.19404f,0.69365f,-0.17608f,0.67097f), new Vector3(1.00000f,1.00000f,1.00000f), true),
        new PointDef("AirUnlock", new Vector3(14.89000f,3.49900f,20.69400f), new Quaternion(0.00000f,0.00000f,0.00000f,1.00000f), new Vector3(1.00000f,1.00000f,1.00000f), true),
        new PointDef("SIDEREAKING TOOL (6)", new Vector3(-6.26900f,0.74400f,-229.28700f), new Quaternion(-0.99923f,0.00000f,0.00000f,0.03922f), new Vector3(2.06130f,2.06130f,75.79227f), false),
        new PointDef("ToolBoxSelection", new Vector3(11.91100f,3.42000f,27.81100f), new Quaternion(0.16765f,0.70050f,-0.15056f,0.67715f), new Vector3(1.00000f,1.00000f,1.00000f), true),
        new PointDef("drop (1)", new Vector3(-2.87700f,0.18600f,38.33800f), new Quaternion(0.00000f,0.00000f,0.00000f,1.00000f), new Vector3(1.00000f,1.00000f,1.00000f), true),
        new PointDef("Normal Pot Operation", new Vector3(-2.88346f,2.54900f,40.40700f), new Quaternion(0.00000f,-0.97629f,0.21646f,0.00000f), new Vector3(1.00000f,1.00000f,1.00000f), true),
        new PointDef("Ideal Pot Voltage", new Vector3(-0.70000f,1.04000f,-66.51000f), new Quaternion(-0.02942f,0.79737f,-0.04461f,-0.60112f), new Vector3(1.00000f,1.00000f,1.00000f), true),
        new PointDef("CBT_LookTarget", new Vector3(2.40000f,2.90300f,-65.53000f), new Quaternion(0.00000f,-0.00549f,0.00000f,0.99998f), new Vector3(1.00000f,1.00000f,1.00000f), true),
        new PointDef("PTM Crane", new Vector3(9.66000f,2.66000f,41.50000f), new Quaternion(0.00000f,0.71292f,0.00000f,-0.70124f), new Vector3(1.00000f,1.00000f,1.00000f), true),
        new PointDef("PotInspection", new Vector3(-2.50100f,2.89500f,-227.51100f), new Quaternion(-0.00163f,0.99970f,0.02447f,0.00004f), new Vector3(1.00000f,1.00000f,1.00000f), true),
        new PointDef("drop (2)", new Vector3(-2.87700f,0.08400f,38.44900f), new Quaternion(0.00000f,0.00000f,0.00000f,1.00000f), new Vector3(1.00000f,1.00000f,1.00000f), true),
        new PointDef("Hoe", new Vector3(12.93200f,2.24800f,-227.22800f), new Quaternion(0.00675f,0.95837f,-0.28442f,0.02426f), new Vector3(1.00000f,1.00000f,1.00000f), true),
        new PointDef("PotControlMachine", new Vector3(-17.21300f,1.95300f,42.52100f), new Quaternion(0.00000f,-1.00000f,0.00000f,0.00000f), new Vector3(3.18135f,3.18134f,3.18134f), true),
        new PointDef("Fused Alumina Bag", new Vector3(-3.13300f,1.73300f,38.99100f), new Quaternion(0.22085f,-0.31596f,-0.09759f,0.91754f), new Vector3(0.22490f,0.31327f,0.31327f), false),
        new PointDef("Tools", new Vector3(5.92000f,3.07000f,-66.90000f), new Quaternion(0.00000f,0.00000f,0.00000f,1.00000f), new Vector3(1.00000f,1.00000f,1.00000f), true),
    };
}
