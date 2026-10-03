package main

import (
	"os"
	"os/exec"
	"path/filepath"
	"strings"
	"syscall"
	"time"
	"unsafe"
)

const (
	upgradeCode = "{8F2A9C41-6B17-4D3E-A805-9C4E1F72B0D6}"
	productName = "Port Killer"

	mbOK           = 0x0
	mbYesNo        = 0x4
	mbIconError    = 0x10
	mbIconQuestion = 0x20
	mbIconInfo     = 0x40
	mbDefButton2   = 0x100
	idYes          = 6

	th32csSnapProcess              = 0x00000002
	processTerminate               = 0x0001
	processQueryLimitedInformation = 0x1000
	synchronize                    = 0x00100000
	maxPath                        = 260

	hkeyCurrentUser      = 0x80000001
	hkeyLocalMachine     = 0x80000002
	hkeyUsers            = 0x80000003
	keyRead              = 0x20019
	regSz                = 1
	errorSuccess         = 0
	errorNoMoreItems     = 259
	moveDelayUntilReboot = 4
)

type processEntry32 struct {
	Size            uint32
	CntUsage        uint32
	ProcessID       uint32
	DefaultHeapID   uintptr
	ModuleID        uint32
	CntThreads      uint32
	ParentProcessID uint32
	PriClassBase    int32
	Flags           uint32
	ExeFile         [maxPath]uint16
}

func main() {
	self, err := os.Executable()
	if err != nil {
		fail(tr("Unable to locate the uninstaller.", "Impossible de localiser le désinstalleur."))
		return
	}
	self, _ = filepath.Abs(self)

	args := parseArgs(os.Args[1:])
	if !args.fromTemp {
		if messageBox(
			tr(
				"Uninstall Port Killer and remove all files, shortcuts, and registry traces?\n\nThis cannot be undone.",
				"Désinstaller Port Killer et supprimer tous les fichiers, raccourcis et traces du registre ?\n\nCette action est irréversible.",
			),
			mbYesNo|mbIconQuestion|mbDefButton2,
		) != idYes {
			return
		}

		installDir := filepath.Dir(self)
		if err := relaunchFromTemp(self, installDir); err != nil {
			fail(tr("Unable to prepare uninstallation.\n", "Impossible de préparer la désinstallation.\n") + err.Error())
		}
		return
	}

	time.Sleep(600 * time.Millisecond)
	killPortKillerProcesses(args.installDir)
	uninstallMsiProducts()
	wipeLeftovers(args.installDir)
	messageBox(
		tr(
			"Port Killer has been uninstalled.\nNo application files were kept.",
			"Port Killer a été désinstallé.\nAucun fichier de l'application n'a été conservé.",
		),
		mbOK|mbIconInfo,
	)
	selfDelete(self)
}

type options struct {
	fromTemp   bool
	installDir string
}

func parseArgs(argv []string) options {
	var opt options
	for _, raw := range argv {
		switch {
		case raw == "--from-temp":
			opt.fromTemp = true
		case strings.HasPrefix(raw, "--install-dir="):
			opt.installDir = strings.TrimPrefix(raw, "--install-dir=")
		}
	}
	return opt
}

func relaunchFromTemp(self, installDir string) error {
	dest := filepath.Join(os.TempDir(), "PortKiller-Uninstaller.exe")
	data, err := os.ReadFile(self)
	if err != nil {
		return err
	}
	if err := os.WriteFile(dest, data, 0o755); err != nil {
		return err
	}

	cmd := exec.Command(dest, "--from-temp", "--install-dir="+installDir)
	cmd.SysProcAttr = &syscall.SysProcAttr{HideWindow: true}
	return cmd.Start()
}

func killPortKillerProcesses(installDir string) {
	if strings.TrimSpace(installDir) == "" {
		// Fail closed: never kill by name alone without an install root.
		return
	}

	kernel32 := syscall.NewLazyDLL("kernel32.dll")
	createSnap := kernel32.NewProc("CreateToolhelp32Snapshot")
	procFirst := kernel32.NewProc("Process32FirstW")
	procNext := kernel32.NewProc("Process32NextW")
	closeHandle := kernel32.NewProc("CloseHandle")
	openProcess := kernel32.NewProc("OpenProcess")
	terminate := kernel32.NewProc("TerminateProcess")
	waitFor := kernel32.NewProc("WaitForSingleObject")
	queryName := kernel32.NewProc("QueryFullProcessImageNameW")

	selfPid := uint32(os.Getpid())
	snap, _, _ := createSnap.Call(th32csSnapProcess, 0)
	if snap == 0 || snap == uintptr(^uintptr(0)) {
		return
	}
	defer closeHandle.Call(snap)

	var entry processEntry32
	entry.Size = uint32(unsafe.Sizeof(entry))
	ok, _, _ := procFirst.Call(snap, uintptr(unsafe.Pointer(&entry)))
	for ok != 0 {
		name := strings.ToLower(syscall.UTF16ToString(entry.ExeFile[:]))
		if entry.ProcessID != selfPid && name == "portkiller.exe" {
			terminatePid(openProcess, terminate, waitFor, queryName, closeHandle, entry.ProcessID, installDir)
		}
		ok, _, _ = procNext.Call(snap, uintptr(unsafe.Pointer(&entry)))
	}
}

func terminatePid(openProcess, terminate, waitFor, queryName, closeHandle *syscall.LazyProc, pid uint32, installDir string) {
	access := uintptr(processTerminate | processQueryLimitedInformation | synchronize)
	handle, _, _ := openProcess.Call(access, 0, uintptr(pid))
	if handle == 0 {
		return
	}
	defer closeHandle.Call(handle)

	var buf [maxPath]uint16
	size := uint32(len(buf))
	queryName.Call(handle, 0, uintptr(unsafe.Pointer(&buf[0])), uintptr(unsafe.Pointer(&size)))
	image := syscall.UTF16ToString(buf[:])
	lowerImage := strings.ToLower(image)
	if strings.Contains(lowerImage, "portkiller-uninstaller.exe") ||
		strings.HasSuffix(lowerImage, `\uninstaller.exe`) ||
		strings.HasSuffix(lowerImage, `/uninstaller.exe`) {
		return
	}

	if !isPathUnderInstallDir(image, installDir) {
		return
	}

	terminate.Call(handle, 1)
	waitFor.Call(handle, 3000)
}

func isPathUnderInstallDir(imagePath, installDir string) bool {
	if strings.TrimSpace(imagePath) == "" || strings.TrimSpace(installDir) == "" {
		return false
	}

	absImage, err := filepath.Abs(imagePath)
	if err != nil {
		return false
	}
	absInstall, err := filepath.Abs(installDir)
	if err != nil {
		return false
	}

	absImage = filepath.Clean(absImage)
	absInstall = filepath.Clean(absInstall)
	prefix := absInstall
	if !strings.HasSuffix(prefix, string(os.PathSeparator)) {
		prefix += string(os.PathSeparator)
	}

	lowerImage := strings.ToLower(absImage)
	lowerPrefix := strings.ToLower(prefix)
	lowerInstall := strings.ToLower(absInstall)
	return lowerImage == lowerInstall || strings.HasPrefix(lowerImage, lowerPrefix)
}

func uninstallMsiProducts() {
	msi := syscall.NewLazyDLL("msi.dll")
	enumRelated := msi.NewProc("MsiEnumRelatedProductsW")
	upgrade, _ := syscall.UTF16PtrFromString(upgradeCode)

	for i := uint32(0); i < 32; i++ {
		var product [39]uint16
		status, _, _ := enumRelated.Call(
			uintptr(unsafe.Pointer(upgrade)),
			0,
			uintptr(i),
			uintptr(unsafe.Pointer(&product[0])),
		)
		if status == errorNoMoreItems {
			break
		}
		if status != errorSuccess {
			continue
		}
		runMsiexec(syscall.UTF16ToString(product[:]))
	}
}

func runMsiexec(productCode string) {
	if productCode == "" {
		return
	}
	cmd := exec.Command("msiexec.exe", "/x", productCode, "/qb", "/norestart")
	_ = cmd.Run()
}

func wipeLeftovers(installDir string) {
	removePath(installDir)
	removePath(`C:\Program Files\Port Killer`)
	removePath(`C:\Program Files (x86)\Port Killer`)
	removePath(`C:\ProgramData\Port Killer`)
	removePath(`C:\ProgramData\PortKiller`)
	removePath(`C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Port Killer`)
	removeFile(`C:\Users\Public\Desktop\Port Killer.lnk`)
	removePrefetch()
	removeRegistryTraces()
	wipeUserProfiles()
}

func wipeUserProfiles() {
	entries, err := os.ReadDir(`C:\Users`)
	if err != nil {
		return
	}
	skip := map[string]bool{
		"Public":       true,
		"Default":      true,
		"Default User": true,
		"All Users":    true,
	}
	for _, entry := range entries {
		if !entry.IsDir() || skip[entry.Name()] {
			continue
		}
		home := filepath.Join(`C:\Users`, entry.Name())
		removePath(filepath.Join(home, `AppData\Local\Port Killer`))
		removePath(filepath.Join(home, `AppData\Local\PortKiller`))
		removePath(filepath.Join(home, `AppData\Roaming\Port Killer`))
		removePath(filepath.Join(home, `AppData\Roaming\PortKiller`))
		removePath(filepath.Join(home, `AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Port Killer`))
		removeFile(filepath.Join(home, `Desktop\Port Killer.lnk`))
		removeFile(filepath.Join(home, `OneDrive\Desktop\Port Killer.lnk`))
		removeGlob(filepath.Join(home, `AppData\Local\CrashDumps`), "PortKiller.exe*.dmp")
	}
}

func removePrefetch() {
	// Only Port Killer prefetch entries — never a generic UNINSTALLER*.pf glob.
	removeGlob(`C:\Windows\Prefetch`, "PORTKILLER*.pf")
}

func removeRegistryTraces() {
	advapi := syscall.NewLazyDLL("advapi32.dll")
	deleteTree := advapi.NewProc("RegDeleteTreeW")
	deleteTreeKey(deleteTree, hkeyLocalMachine, `Software\PortKiller`)
	deleteTreeKey(deleteTree, hkeyLocalMachine, `Software\WOW6432Node\PortKiller`)
	deleteTreeKey(deleteTree, hkeyCurrentUser, `Software\PortKiller`)
	deleteUninstallEntries(deleteTree)
	deleteFromLoadedUserHives(deleteTree)
}

func deleteUninstallEntries(deleteTree *syscall.LazyProc) {
	roots := []string{
		`SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall`,
		`SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall`,
	}
	for _, root := range roots {
		for _, sub := range childKeys(hkeyLocalMachine, root) {
			display := readRegString(hkeyLocalMachine, root+`\`+sub, "DisplayName")
			if display == productName {
				deleteTreeKey(deleteTree, hkeyLocalMachine, root+`\`+sub)
			}
		}
	}
}

func deleteFromLoadedUserHives(deleteTree *syscall.LazyProc) {
	for _, sid := range childKeys(hkeyUsers, "") {
		if sid == "" || sid == ".DEFAULT" || strings.HasSuffix(strings.ToUpper(sid), "_CLASSES") {
			continue
		}
		deleteTreeKey(deleteTree, hkeyUsers, sid+`\Software\PortKiller`)
	}
}

func deleteTreeKey(deleteTree *syscall.LazyProc, root uintptr, subKey string) {
	ptr, err := syscall.UTF16PtrFromString(subKey)
	if err != nil {
		return
	}
	deleteTree.Call(root, uintptr(unsafe.Pointer(ptr)))
}

func childKeys(root uintptr, subKey string) []string {
	advapi := syscall.NewLazyDLL("advapi32.dll")
	openKey := advapi.NewProc("RegOpenKeyExW")
	enumKey := advapi.NewProc("RegEnumKeyExW")
	closeKey := advapi.NewProc("RegCloseKey")

	var subPtr *uint16
	if subKey != "" {
		subPtr, _ = syscall.UTF16PtrFromString(subKey)
	}
	var handle uintptr
	status, _, _ := openKey.Call(root, uintptr(unsafe.Pointer(subPtr)), 0, keyRead, uintptr(unsafe.Pointer(&handle)))
	if status != errorSuccess {
		return nil
	}
	defer closeKey.Call(handle)

	var names []string
	for i := uint32(0); i < 4096; i++ {
		var name [256]uint16
		nameLen := uint32(len(name))
		st, _, _ := enumKey.Call(handle, uintptr(i), uintptr(unsafe.Pointer(&name[0])), uintptr(unsafe.Pointer(&nameLen)), 0, 0, 0, 0)
		if st == errorNoMoreItems {
			break
		}
		if st != errorSuccess {
			continue
		}
		names = append(names, syscall.UTF16ToString(name[:]))
	}
	return names
}

func readRegString(root uintptr, subKey, name string) string {
	advapi := syscall.NewLazyDLL("advapi32.dll")
	openKey := advapi.NewProc("RegOpenKeyExW")
	queryValue := advapi.NewProc("RegQueryValueExW")
	closeKey := advapi.NewProc("RegCloseKey")

	subPtr, _ := syscall.UTF16PtrFromString(subKey)
	namePtr, _ := syscall.UTF16PtrFromString(name)
	var handle uintptr
	status, _, _ := openKey.Call(root, uintptr(unsafe.Pointer(subPtr)), 0, keyRead, uintptr(unsafe.Pointer(&handle)))
	if status != errorSuccess {
		return ""
	}
	defer closeKey.Call(handle)

	var valType uint32
	var size uint32
	status, _, _ = queryValue.Call(handle, uintptr(unsafe.Pointer(namePtr)), 0, uintptr(unsafe.Pointer(&valType)), 0, uintptr(unsafe.Pointer(&size)))
	if status != errorSuccess || valType != regSz || size == 0 {
		return ""
	}
	buf := make([]uint16, size/2)
	status, _, _ = queryValue.Call(handle, uintptr(unsafe.Pointer(namePtr)), 0, uintptr(unsafe.Pointer(&valType)), uintptr(unsafe.Pointer(&buf[0])), uintptr(unsafe.Pointer(&size)))
	if status != errorSuccess {
		return ""
	}
	return syscall.UTF16ToString(buf)
}

func removePath(path string) {
	if path == "" {
		return
	}
	_ = os.RemoveAll(path)
}

func removeFile(path string) {
	_ = os.Remove(path)
}

func removeGlob(dir, pattern string) {
	matches, err := filepath.Glob(filepath.Join(dir, pattern))
	if err != nil {
		return
	}
	for _, match := range matches {
		_ = os.Remove(match)
	}
}

func selfDelete(self string) {
	kernel32 := syscall.NewLazyDLL("kernel32.dll")
	moveFileEx := kernel32.NewProc("MoveFileExW")
	selfPtr, _ := syscall.UTF16PtrFromString(self)
	moveFileEx.Call(uintptr(unsafe.Pointer(selfPtr)), 0, moveDelayUntilReboot)

	cmd := exec.Command("cmd.exe", "/C", `ping 127.0.0.1 -n 2 >nul & del /f /q "`+self+`"`)
	cmd.SysProcAttr = &syscall.SysProcAttr{HideWindow: true}
	_ = cmd.Start()
}

func fail(text string) {
	messageBox(text, mbOK|mbIconError)
	os.Exit(1)
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

func messageBox(text string, flags uintptr) int {
	user32 := syscall.NewLazyDLL("user32.dll")
	proc := user32.NewProc("MessageBoxW")
	textPtr, _ := syscall.UTF16PtrFromString(text)
	captionPtr, _ := syscall.UTF16PtrFromString("Port Killer")
	ret, _, _ := proc.Call(0, uintptr(unsafe.Pointer(textPtr)), uintptr(unsafe.Pointer(captionPtr)), flags)
	return int(ret)
}
