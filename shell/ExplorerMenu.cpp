// Explorer right-click command for the Windows 11 context menu (top level / "App extensions").
//
// Windows 11 only puts IExplorerCommand handlers from apps with package identity in its modern menu;
// classic registry verbs stay under "Show more options". This DLL is that handler. The installer registers
// it through a signed sparse package (see AppxManifest.xml), and all it does is launch the app with the
// selected paths, the same command line the classic verb uses.
//
// The same file is used by Renamerama and VideoSqueeze; MenuConfig.h holds the per-app bits.

#include <windows.h>
#include <shlobj_core.h>
#include <shobjidl_core.h>
#include <shlwapi.h>
#include <wrl/client.h>
#include <wrl/implements.h>
#include <wrl/module.h>

#include <string>
#include <vector>

#include "MenuConfig.h"

using Microsoft::WRL::ClassicCom;
using Microsoft::WRL::ComPtr;
using Microsoft::WRL::InProc;
using Microsoft::WRL::Module;
using Microsoft::WRL::RuntimeClass;
using Microsoft::WRL::RuntimeClassFlags;

namespace
{
    // Explorer's command lines top out at 32,767 characters; stay well under and start more processes
    // for huge selections (the app's single-instance pipe merges them into one window).
    constexpr size_t MaxCommandLine = 30000;

    std::wstring ExePath()
    {
        HMODULE module{};
        GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                           reinterpret_cast<LPCWSTR>(&ExePath), &module);
        std::wstring path(MAX_PATH, L'\0');
        for (;;)
        {
            DWORD length = GetModuleFileNameW(module, path.data(), static_cast<DWORD>(path.size()));
            if (length < path.size())
            {
                path.resize(length);
                break;
            }
            path.resize(path.size() * 2);
        }
        path.resize(path.find_last_of(L'\\') + 1);
        return path + MENU_EXE; // the app sits next to this DLL
    }

    // Quotes one argument the way CommandLineToArgvW and .NET parse it back.
    std::wstring Quote(const std::wstring& arg)
    {
        std::wstring quoted = L"\"";
        size_t backslashes = 0;
        for (wchar_t c : arg)
        {
            if (c == L'\\')
            {
                ++backslashes;
                continue;
            }
            quoted.append(c == L'"' ? backslashes * 2 + 1 : backslashes, L'\\');
            backslashes = 0;
            quoted += c;
        }
        quoted.append(backslashes * 2, L'\\'); // keep e.g. "C:\" from escaping the closing quote
        quoted += L'"';
        return quoted;
    }

    void Start(std::wstring commandLine)
    {
        std::wstring exe = ExePath();
        STARTUPINFOW startup{sizeof(startup)};
        PROCESS_INFORMATION process{};
        if (CreateProcessW(exe.c_str(), commandLine.data(), nullptr, nullptr, FALSE, 0, nullptr, nullptr, &startup,
                           &process))
        {
            CloseHandle(process.hThread);
            CloseHandle(process.hProcess);
        }
    }

    // Launches the app with these arguments, split over several processes if they don't fit in one.
    void Launch(const std::vector<std::wstring>& args)
    {
        const std::wstring exe = Quote(ExePath());
        std::wstring commandLine = exe;
        bool hasArgs = false;
        for (const auto& arg : args)
        {
            std::wstring next = L" " + arg;
            if (hasArgs && commandLine.size() + next.size() > MaxCommandLine)
            {
                Start(commandLine);
                commandLine = exe;
            }
            commandLine += next;
            hasArgs = true;
        }
        if (hasArgs)
            Start(commandLine);
    }

    std::wstring PathOf(IShellItem* item)
    {
        PWSTR path = nullptr;
        if (FAILED(item->GetDisplayName(SIGDN_FILESYSPATH, &path)))
            return {}; // not a file system item (e.g. inside a phone or zip)
        std::wstring result = path;
        CoTaskMemFree(path);
        return result;
    }

    std::vector<std::wstring> PathsOf(IShellItemArray* items)
    {
        std::vector<std::wstring> paths;
        DWORD count = 0;
        if (!items || FAILED(items->GetCount(&count)))
            return paths;
        for (DWORD i = 0; i < count; ++i)
        {
            ComPtr<IShellItem> item;
            if (SUCCEEDED(items->GetItemAt(i, &item)))
                if (auto path = PathOf(item.Get()); !path.empty())
                    paths.push_back(std::move(path));
        }
        return paths;
    }

    // The folder whose empty space was right-clicked, from the Explorer window hosting the menu.
    std::wstring FolderOfSite(IUnknown* site)
    {
        ComPtr<IServiceProvider> services;
        ComPtr<IShellBrowser> browser;
        ComPtr<IShellView> view;
        ComPtr<IFolderView> folderView;
        ComPtr<IShellItem> folder;
        if (site && SUCCEEDED(site->QueryInterface(IID_PPV_ARGS(&services))) &&
            SUCCEEDED(services->QueryService(SID_STopLevelBrowser, IID_PPV_ARGS(&browser))) &&
            SUCCEEDED(browser->QueryActiveShellView(&view)) && SUCCEEDED(view.As(&folderView)) &&
            SUCCEEDED(folderView->GetFolder(IID_PPV_ARGS(&folder))))
            return PathOf(folder.Get());
        return {};
    }

    template <bool Background>
    class Command : public RuntimeClass<RuntimeClassFlags<ClassicCom>, IExplorerCommand, IObjectWithSite>
    {
        ComPtr<IUnknown> m_site;

    public:
        IFACEMETHODIMP GetTitle(IShellItemArray*, PWSTR* name) override { return SHStrDupW(MENU_TITLE, name); }

        IFACEMETHODIMP GetIcon(IShellItemArray*, PWSTR* icon) override
        {
            return SHStrDupW((ExePath() + L",0").c_str(), icon);
        }

        IFACEMETHODIMP GetToolTip(IShellItemArray*, PWSTR* tip) override
        {
            *tip = nullptr;
            return E_NOTIMPL;
        }

        IFACEMETHODIMP GetCanonicalName(GUID* name) override
        {
            *name = GUID_NULL;
            return S_OK;
        }

        IFACEMETHODIMP GetState(IShellItemArray*, BOOL, EXPCMDSTATE* state) override
        {
            *state = ECS_ENABLED;
            return S_OK;
        }

        IFACEMETHODIMP GetFlags(EXPCMDFLAGS* flags) override
        {
            *flags = ECF_DEFAULT;
            return S_OK;
        }

        IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** commands) override
        {
            *commands = nullptr;
            return E_NOTIMPL;
        }

        IFACEMETHODIMP Invoke(IShellItemArray* items, IBindCtx*) override
        {
            std::vector<std::wstring> args;
            if constexpr (Background)
            {
                // Explorer usually passes the folder itself; fall back to asking the window.
                auto paths = PathsOf(items);
                std::wstring folder = paths.empty() ? FolderOfSite(m_site.Get()) : paths.front();
                if (folder.empty())
                    return S_OK;
#ifdef MENU_BACKGROUND_ARG
                args.push_back(MENU_BACKGROUND_ARG);
#endif
                args.push_back(Quote(folder));
            }
            else
            {
                for (const auto& path : PathsOf(items))
                    args.push_back(Quote(path));
                if (args.empty())
                    return S_OK;
            }
            Launch(args);
            return S_OK;
        }

        IFACEMETHODIMP SetSite(IUnknown* site) override
        {
            m_site = site;
            return S_OK;
        }

        IFACEMETHODIMP GetSite(REFIID riid, void** site) override
        {
            *site = nullptr;
            return m_site ? m_site.CopyTo(riid, site) : E_FAIL;
        }
    };
}

// Files and folders.
class __declspec(uuid(MENU_CLSID)) ItemsCommand final : public Command<false>
{
};
CoCreatableClass(ItemsCommand)

#ifdef MENU_BACKGROUND_CLSID
// Empty space inside a folder.
class __declspec(uuid(MENU_BACKGROUND_CLSID)) BackgroundCommand final : public Command<true>
{
};
CoCreatableClass(BackgroundCommand)
#endif

STDAPI DllGetClassObject(REFCLSID clsid, REFIID riid, void** object)
{
    return Module<InProc>::GetModule().GetClassObject(clsid, riid, object);
}

STDAPI DllCanUnloadNow()
{
    return Module<InProc>::GetModule().Terminate() ? S_OK : S_FALSE;
}

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
        DisableThreadLibraryCalls(module);
    return TRUE;
}
