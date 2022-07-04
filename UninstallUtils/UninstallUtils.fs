module UninstallUtils

open System
open System.Diagnostics
open System.IO
open System.Reflection
open System.Runtime.InteropServices
open System.Security.AccessControl
open System.Threading
open Microsoft.Win32

let private DisplayNameKey = "DisplayName"
let private UninstallStringKey = "UninstallString"

let private tryGetUninstallRegKey productName =
  match Registry.CurrentUser.OpenSubKey
         @"Software\Microsoft\Windows\CurrentVersion\Uninstall" with
   | null -> None
   | subKey ->
     subKey.GetSubKeyNames ()
     |> Seq.tryPick (fun name ->
        match subKey.OpenSubKey
               (name,
                RegistryKeyPermissionCheck.ReadWriteSubTree,
                RegistryRights.QueryValues
                ||| RegistryRights.ReadKey
                ||| RegistryRights.SetValue) with
         | null -> None
         | app ->
           match app.GetValue DisplayNameKey with
            | :? string as str when str = productName -> Some app
            | _ -> None)

let private tryGetUninstallExePath () =
  let exe =
    Path.Combine
     (Assembly.GetExecutingAssembly().Location
      |> Path.GetDirectoryName,
      "uninstall.exe")
  if File.Exists exe then Some exe else None

let private originalInstallPrefix = "rundll32.exe"

let private trySplitToFileAndArgs (uninstallString: string) =
  if not (uninstallString.StartsWith originalInstallPrefix) ||
     uninstallString.Contains "\"" then
    None
  else
    (uninstallString.Substring (0, originalInstallPrefix.Length),
     uninstallString.Substring (originalInstallPrefix.Length + 1)) |> Some

let private tryGetOriginalUninstallString (uninstallRegKey: RegistryKey) =
  match uninstallRegKey.GetValue UninstallStringKey with
   | :? string as str when trySplitToFileAndArgs str |> Option.isSome -> Some str
   | _ -> None

///////////////////////////////////////////////////////////////////////////////

type UninstallSetupInfo = {
    ApplicationFolder: string
    ProductName: string
    UninstallRegKey: RegistryKey
    UninstallString: string
    UninstallExePath: string
  }

let tryGetUninstallSetupInfo applicationFolder productName =
  let (>>=) xO x2yO = Option.bind x2yO xO
  tryGetUninstallRegKey productName >>= fun uninstallRegKey ->
  tryGetOriginalUninstallString uninstallRegKey >>= fun uninstallString ->
  tryGetUninstallExePath () >>= fun uninstallExePath ->
  {ApplicationFolder = applicationFolder
   ProductName = productName
   UninstallRegKey = uninstallRegKey
   UninstallString = uninstallString
   UninstallExePath = uninstallExePath} |> Some

let setupUninstall setup =
  setup.UninstallRegKey.SetValue ("NoModify", 1, RegistryValueKind.DWord)
  setup.UninstallRegKey.SetValue ("NoRepair", 1, RegistryValueKind.DWord)
  setup.UninstallRegKey.SetValue
   (UninstallStringKey,
    sprintf "\"%s\" uninstall \"%s\" \"%s\" \"%s\""
     setup.UninstallExePath
     setup.ApplicationFolder
     setup.ProductName
     setup.UninstallString)

///////////////////////////////////////////////////////////////////////////////

type UninstallInfo = {
    ProductName: string
    UninstallFile: string
    UninstallArgs: string
  }

let tryGetUninstallInfo applicationFolder productName uninstallString =
  let (>>=) xO x2yO = Option.bind x2yO xO
  trySplitToFileAndArgs uninstallString >>= fun (file, args) ->
  {ProductName = productName
   UninstallFile = file
   UninstallArgs = args} |> Some

[<DllImport("user32.dll")>]
extern bool SetForegroundWindow(IntPtr hWnd)
[<DllImport("user32.dll", SetLastError = true)>]
extern bool PostMessage(IntPtr hWnd,
                        [<MarshalAs(UnmanagedType.U4)>] uint32 Msg,
                        IntPtr wParam,
                        IntPtr lParam)

let WM_KEYDOWN = 0x0100u
let WM_KEYUP = 0x0101u

module private Keys =
  let Shift = 65536
  let LButton = 1
  let Back = 8
  let Tab = Back ||| LButton
  let Space = 32
  let Down = Space ||| Back
  let MButton = 4
  let Clear = Back ||| MButton
  let Enter = Clear ||| Tab

let killProcesses info =
  Process.GetProcessesByName info.ProductName
  |> Seq.iter (fun proc -> proc.Kill ())

let startUninstallString info =
  let proc =
    new Process (StartInfo = ProcessStartInfo (Arguments = info.UninstallArgs,
                                               FileName = info.UninstallFile,
                                               UseShellExecute = false))
  proc.Start () |> ignore

let tryRespondToUninstaller info =
  let rec tryRespondToClickOnce = function
   | 0 -> ()
   | attempts ->
     Thread.Sleep 150
     Process.GetProcessesByName "dfsvc"
     |> Seq.tryPick (fun proc ->
        if not (String.IsNullOrEmpty proc.MainWindowTitle) &&
           proc.MainWindowTitle.StartsWith info.ProductName
        then Some proc.MainWindowHandle else None)
     |> function
         | None -> tryRespondToClickOnce (attempts - 1)
         | Some windowHandle ->
           if not (SetForegroundWindow windowHandle) then
             tryRespondToClickOnce (attempts - 1)
           else
             Thread.Sleep 100
             let post keys =
               PostMessage (windowHandle, WM_KEYDOWN, nativeint keys, 0n)
             ignore (post (Keys.Shift ||| Keys.Tab)
                  && post (Keys.Shift ||| Keys.Tab)
                  && post Keys.Down
                  && post Keys.Tab
                  && post Keys.Enter)
  tryRespondToClickOnce 250
