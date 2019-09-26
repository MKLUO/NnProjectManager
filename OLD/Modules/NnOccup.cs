// using System;
// using System.Collections.Generic;
// using System.Collections.Immutable;
// using System.IO;
// using System.Linq;
// using System.Threading;


// #nullable enable

// namespace NnManager {
//     using RPath = Util.RestrictedPath; 

//     partial class NnTask {   

//         RPath NnOccupPath => FSPath.SubPath("NnOccup");
//         RPath NnOccupResultPath => NnOccupPath.SubPath("occup.txt");

//         NnModule NnOccup(ImmutableDictionary<string, string> options) =>
//             new NnModule(
//                 "NN Occupation",
//                 NnOccupCanExecute, 
//                 NnOccupIsDone, 
//                 NnOccupExecute, 
//                 NnOccupGetResult, 
//                 NnModule.GetDefaultOptions(ModuleType.NnOccup),
//                 options);

//         // FIXME:
//         public static ImmutableDictionary<string, string> NnOccupDefaultOption => 
//             new Dictionary<string, string>{
//                 {"portion", "0.7"},
//                 {"x0", "-"},
//                 {"y0", "-"},
//                 {"z0", "-"},
//                 {"x1", "-"},
//                 {"y1", "-"},
//                 {"z1", "-"}
//             }.ToImmutableDictionary();

//         bool NnOccupCanExecute() {
//             return NnMainIsDone();
//         }

//         bool NnOccupIsDone() {
//             return File.Exists(NnOccupResultPath);
//         }

//         string NnOccupGetResult() {
//             try {
//                 return File.ReadAllText(NnOccupResultPath);
//             } catch {
//                 return "(error)";
//             }
//         }

//         bool NnOccupExecute(CancellationToken ct, ImmutableDictionary<string, string> options) {
//             // FIXME: HACKING HERE! Should be generalized in future.
            
//             // RPath? spectrumFile = FSPath.SubPath("wf_spectrum_quantum_region_X1.dat");
//             // if (spectrumFile?.Content == null) 
//             //     return false;

//             // Dictionary<int, double>? spectrum = NnAgent.ReadNXY(spectrumFile.Content, 1);
//             // if (spectrum == null) return false;

//             // double result = 0.0;

//             // int counter = 0;
//             // RPath? data, coord;
//             // while( 
//             //     ((data, coord) = 
//             //         NnAgent.GetCoordAndDat(
//             //             FSPath, 
//             //             $"wf_probability_quantum_region_X1_0000_{counter++.ToString("0000")}"
//             //         )) != (null, null)
//             // ) {
//             //     if ((data?.Content == null) || (coord?.Content == null))
//             //         break;

//             //     ScalarField field = ScalarField.FromNnDatAndCoord(data.Content, coord.Content);

//             //     options.TryGetValue("portion", out string? portion);
//             //     options.TryGetValue("x0", out string? x0);
//             //     options.TryGetValue("x1", out string? x1);
//             //     options.TryGetValue("y0", out string? y0);
//             //     options.TryGetValue("y1", out string? y1);
//             //     options.TryGetValue("z0", out string? z0);
//             //     options.TryGetValue("z1", out string? z1);

//             //     if (field.PortionInRange(
//             //             (
//             //                 x0 != "-" ? Convert.ToDouble(x0) : (double?)null, 
//             //                 y0 != "-" ? Convert.ToDouble(y0) : (double?)null, 
//             //                 z0 != "-" ? Convert.ToDouble(z0) : (double?)null
//             //             ), 
//             //             (
//             //                 x1 != "-" ? Convert.ToDouble(x1) : (double?)null, 
//             //                 y1 != "-" ? Convert.ToDouble(y1) : (double?)null, 
//             //                 z1 != "-" ? Convert.ToDouble(z1) : (double?)null
//             //             )
//             //         ) > Convert.ToDouble(portion ?? "0.75"))
//             //         result += spectrum[counter];
//             // }

//             // // TODO: Spectrum annotation
//             // File.WriteAllText(NnOccupResultPath, result.ToString());
//             return true;
//         }

//         
// }