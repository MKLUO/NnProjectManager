using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;


#nullable enable

namespace NnManager {
    using RPath = Util.RestrictedPath; 

    partial class NnTask {
        
        RPath NnLaplacianPath       => FSPath.SubPath("NnLaplacian");
        RPath NnLaplacianFilePath   => NnLaplacianPath.SubPath("_Report.txt");

        NnModule NnLaplacion(ImmutableDictionary<string, string> options) =>
            new NnModule(
                "NN Laplacian",
                NnLaplacianCanExecute, 
                NnLaplacianIsDone, 
                NnLaplacianExecute, 
                NnLaplacianGetResult, 
                NnLaplacianDefaultOption,
                options);

        public static ImmutableDictionary<string, string> NnLaplacianDefaultOption => 
            new Dictionary<string, string>{
                // {"portion", "0.7"},
                // {"L_x0", "-"}, {"L_y0", "-"}, {"L_z0", "-"}, {"L_x1", "-"}, {"L_y1", "-"}, {"L_z1", "-"},
                // {"band", "X3"},
                // {"non_SC", "no"}
            }.ToImmutableDictionary();

        bool NnLaplacianCanExecute() => NnMainIsDone();

        bool NnLaplacianIsDone() => File.Exists(NnLaplacianFilePath);        

        string NnLaplacianGetResult() => File.ReadAllText(NnLaplacianFilePath);

        bool NnLaplacianExecute(CancellationToken ct, ImmutableDictionary<string, string> options) {
            /*

             Yb    dP    db    88     88 8888b.     db    888888 888888     88 88b 88 88""Yb 88   88 888888 
              Yb  dP    dPYb   88     88  8I  Yb   dPYb     88   88__       88 88Yb88 88__dP 88   88   88   
               YbdP    dP__Yb  88  .o 88  8I  dY  dP__Yb    88   88""       88 88 Y88 88"""  Y8   8P   88   
                YP    dP""""Yb 88ood8 88 8888Y"  dP""""Yb   88   888888     88 88  Y8 88     `YbodP'   88   

            */
            // 1. Check if the nnmain run is a simulation of SQD device
            // 2. Check if the simulation converge
            // 3. Check if the SQD is well formed
            // 4. Check if the scalarfield of potential profila is square-grided 
            
            /*

             88 88b 88 88""Yb 88   88 888888                                                 
             88 88Yb88 88__dP 88   88   88                                                   
             88 88 Y88 88"""  Y8   8P   88                                                   
             88 88  Y8 88     `YbodP'   88                                                   

            */
            // Get scalarfield from bandedge file
            
            // TODO: Add support for selection between NnMain and NnMainNonSC.
            var NNPath = FSPath;

            (var potentialData, var potentialCoord, _, _) = NnAgent.GetCoordAndDat(
                NNPath, "potential_2d_dot_region");
            if ((potentialData?.Content == null) || (potentialCoord?.Content == null)) return false;
            ScalarField potential = ScalarField.FromNnDatAndCoord(potentialData.Content, potentialCoord.Content);

            /*

             88        db    88""Yb 88        db     dP""b8 88    db    88b 88 
             88       dPYb   88__dP 88       dPYb   dP   `" 88   dPYb   88Yb88 
             88  .o  dP__Yb  88"""  88  .o  dP__Yb  Yb      88  dP__Yb  88 Y88 
             88ood8 dP""""Yb 88     88ood8 dP""""Yb  YboodP 88 dP""""Yb 88  Y8 

            */
            // Laplacian of discrete potential profile

            var ftResult = ScalarField.FFT(potential.Data);
            var ifftResult = ScalarField.IFFT(ftResult);

            var diff = ((new ScalarField(ifftResult, potential.Coords)) + (-1) * potential).Sum();

            /*

            888888 88 88     888888 888888 88""Yb 88 88b 88  dP""b8 
            88__   88 88       88   88__   88__dP 88 88Yb88 dP   `" 
            88""   88 88  .o   88   88""   88"Yb  88 88 Y88 Yb  "88 
            88     88 88ood8   88   888888 88  Yb 88 88  Y8  YboodP 

            */
            // Apply filtering to laplacian of potential profile

            /*
            
              dP"Yb  88   88 888888 88""Yb 88   88 888888 
             dP   Yb 88   88   88   88__dP 88   88   88   
             Yb   dP Y8   8P   88   88"""  Y8   8P   88   
              YbodP  `YbodP'   88   88     `YbodP'   88   
            
            */            
            // utput to a text file
            return true;        
        }
    }
}