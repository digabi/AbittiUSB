module Uninstall

open System
open System.Threading
open System.IO
open System.Windows.Forms

[<EntryPoint; STAThread>]
let main argv =
  match argv with
   | [|"uninstall"; applicationFolder; productName; uninstallString|] ->
     match UninstallUtils.tryGetUninstallInfo applicationFolder productName uninstallString with
      | None ->
        MessageBox.Show
         ("Virhe", "Asennustietoja ei löytynyt.", MessageBoxButtons.OK) |> ignore
        1
      | Some info ->
        UninstallUtils.killProcesses info
        Thread.Sleep 250
        Directory.Delete (applicationFolder, true)
        UninstallUtils.startUninstallString info
        UninstallUtils.tryRespondToUninstaller info
        0
   | _ ->
     1
