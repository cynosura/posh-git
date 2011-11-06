#I "./packages/FAKE.1.56.7/tools"
#r "FakeLib.dll"

open Fake 
open System.IO

// properties
let projectName    = "PoshGit"
let version        = "0.3"
let projectSummary = ""
let projectDescription = ""
let authors        = ["";"Ray Glover"]
let mail           = "rayglover@gmail.com"
let homepage       = "http://github.com/cynosura/posh-git"
let license        = ""

// directories
let buildDir       = "./build/"
let packagesDir    = "./packages/"
let deployDir      = "./deploy/"
let shellDir       = "./shell"

let targetPlatformDir  = getTargetPlatformDir "4.0.30319"

// params
let target = getBuildParamOrDefault "target" "All"

// tools
let fakePath = "./packages/FAKE.1.56.7/tools"

// files
let appReferences =
    !+ "./src/**/*.csproj"
        |> Scan

let shellFiles = 
    !+ "./shell/**/*.*"
        |> Scan


let filesToZip =
    !+ (buildDir + "/**/*.*")
        -- "*.zip"
        |> Scan

// targets
Target "Clean" (fun _ ->
    CleanDirs [buildDir; deployDir]
)

Target "BuildApp" (fun _ ->
    AssemblyInfo (fun p ->
        {p with 
            CodeLanguage = CSharp
            AssemblyVersion = version
            AssemblyTitle = projectSummary
            AssemblyDescription = projectDescription
            Guid = "020697d7-24a3-4ce4-a326-d2c7c204ffde"
            OutputFileName = "./src/AssemblyInfo.cs" })

    MSBuildRelease buildDir "Build" appReferences
        |> Log "AppBuild-Output: "
)

Target "CopyShell" (fun _ ->
    shellFiles |> CopyTo buildDir
)


Target "Deploy" (fun _ ->
    !+ (buildDir + "/**/*.*")
        -- "*.zip"
        |> Scan
        |> Zip buildDir (deployDir + sprintf "%s-%s.zip" projectName version)
)

Target "All" DoNothing

// Build order
"Clean"
  ==> "BuildApp" <=> "CopyShell"
  ==> "Deploy"

"All" <== ["Deploy"]

// Start build
Run target

