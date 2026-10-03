package main

import (
	"os"
	"os/exec"
	"path/filepath"
	"syscall"
	"unsafe"
)

func main() {
	self, err := os.Executable()
	if err != nil {
		messageBox(tr("Unable to locate PortKiller.exe.", "Impossible de localiser PortKiller.exe."))
		os.Exit(1)
	}

	root := filepath.Dir(self)
	runtimeDir := filepath.Join(root, "runtime")
	appExe := filepath.Join(runtimeDir, "PortKiller.exe")

	if _, err := os.Stat(appExe); err != nil {
		messageBox(tr(
			"Unable to find runtime\\PortKiller.exe.\nExtract the portable archive without removing the runtime folder.",
			"Impossible de trouver runtime\\PortKiller.exe.\nExtrayez l'archive portable sans supprimer le dossier runtime.",
		))
		os.Exit(1)
	}

	cmd := exec.Command(appExe, os.Args[1:]...)
	cmd.Dir = runtimeDir
	cmd.Stdin = os.Stdin
	cmd.Stdout = os.Stdout
	cmd.Stderr = os.Stderr

	if err := cmd.Start(); err != nil {
		messageBox(tr("Unable to start Port Killer.\n", "Impossible de démarrer Port Killer.\n") + err.Error())
		os.Exit(1)
	}
}

func messageBox(text string) {
	user32 := syscall.NewLazyDLL("user32.dll")
	proc := user32.NewProc("MessageBoxW")
	textPtr, _ := syscall.UTF16PtrFromString(text)
	captionPtr, _ := syscall.UTF16PtrFromString("Port Killer")
	_, _, _ = proc.Call(
		0,
		uintptr(unsafe.Pointer(textPtr)),
		uintptr(unsafe.Pointer(captionPtr)),
		0x10,
	)
}

func tr(english, french string) string {
	if isFrenchUI() {
		return french
	}
	return english
}

func isFrenchUI() bool {
	kernel32 := syscall.NewLazyDLL("kernel32.dll")
	proc := kernel32.NewProc("GetUserDefaultUILanguage")
	lang, _, _ := proc.Call()
	return lang&0x3FF == 0x0C
}
