#r @"C:\Users\Ray\Documents\WindowsPowerShell\Modules\PoshGit\PoshGit.dll";;
#r @"C:\Windows\assembly\GAC_MSIL\System.Management.Automation\1.0.0.0__31bf3856ad364e35\System.Management.Automation.dll";;

open System
open System.Management.Automation;
open System.Collections.Generic
open System.IO
open PoshGit

let cols = [| new ColumnDefinition ("col1", 15); 
              new ColumnDefinition ("col2", 15, Alignment.Center); |]

let table = new PSTable(cols)

table.PrintHeadersToStdOut()
table.PrintLineToStdOut ("aaaaa", "bbbbb");;

let paths = [| @"AAA\BBB\XXX\YYY\" ; 
       @"AAA\BBB\XXX\YYY\ZZZ\" ;
       @"AAA\BBB\XXX\YYY\WWW\" ;
       @"AAA\BBB\XXX\YYY\a file.txt" ;
       @"TTT\YYY\";
       @"a\b\c\"; @"a\bc"; @"aa"; @"a\" |];;

array.Sort paths, (fun x y -> 
            Uri.Compare(new Uri(x), new Uri(y), 
                        UriComponents.Path,
                        UriFormat.Unescaped, 
                        StringComparison.OrdinalIgnoreCase));;

paths;;



let d = new DirectoryInfo @"G:\Dev\Projects\Personal\CSharp\Scratchpad.git"

let printLine text = 
    printf "%s\r\n" text
    false


GitTree.DrawTree (d, 
    [| @"AAA\BBB\XXX\YYY\" ; 
       @"AAA\BBB\XXX\YYY\ZZZ\" ;
       @"AAA\BBB\XXX\YYY\WWW\" ;
       @"TTT\YYY\" |],
    (fun x c -> printLine x))
 

