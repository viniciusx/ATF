﻿//Copyright © 2014 Sony Computer Entertainment America LLC. See License.txt.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Windows.Forms;

using Sce.Atf.Adaptation;

namespace Sce.Atf.Applications
{
    /// <summary>
    /// Component that provides the default commands related to document tab Controls,
    /// such as those that appear in the context menu when right-clicking on a tab</summary>
    /// <remarks>To customize, there are two choices:
    /// 1. To remove or re-order the default commands, create a new class that derives from this
    /// one that overrides whatever methods it needs to. Add it to the plugins list.
    /// 2. To simply add new commands, you can create a new CommandPlugin that overrides
    /// GetPopupCommandTags(). See the implementation below for how to determine if a tab control
    /// was right-clicked on.</remarks>
    [Export(typeof(DefaultTabCommands))]
    [Export(typeof(IInitializable))]
    [Export(typeof(IContextMenuCommandProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class DefaultTabCommands : ICommandClient, IInitializable, IContextMenuCommandProvider
    {
        /// <summary>
        /// Constructor</summary>
        /// <param name="commandService">Command service</param>
        /// <param name="controlHostService">Control hosting service</param>
        /// <param name="controlRegistry">Control registry</param>
        [ImportingConstructor]
        public DefaultTabCommands(
            ICommandService commandService,
            IControlHostService controlHostService,
            IControlRegistry controlRegistry)
        {
            m_commandService = commandService;
            m_controlHostService = controlHostService;
            m_controlRegistry = controlRegistry;
        }

        private enum Command
        {
            CloseCurrentTab,
            CloseOtherTabs,
            CopyFullPath,
            OpenContainingFolder
        }

        public virtual void Initialize()
        {
            m_commandService.RegisterCommand(
                    Command.CloseCurrentTab,
                    null,
                    null,
                    "Close".Localize(),
                    "Closes the current Tab panel".Localize(),
                    Keys.None,
                    null,
                    CommandVisibility.ContextMenu,
                    this);

            m_commandService.RegisterCommand(
                Command.CloseOtherTabs,
                null,
                null,
                "Close All But This".Localize(),
                "Closes all but the current Tab panel".Localize(),
                Keys.None,
                null,
                CommandVisibility.ContextMenu,
                this);

            m_commandService.RegisterCommand(
                    Command.CopyFullPath,
                    null,
                    null,
                    "Copy Full Path".Localize(),
                    "Copies the file path for the document".Localize(),
                    Keys.None,
                    null,
                    CommandVisibility.ContextMenu,
                    this);

            m_commandService.RegisterCommand(
                Command.OpenContainingFolder,
                null,
                null,
                "Open Containing Folder".Localize(),
                "Opens the folder containing the document in Windows Explorer".Localize(),
                Keys.None,
                null,
                CommandVisibility.ContextMenu,
                this);
        }

        #region IContextMenuCommandProvider Members

        /// <summary>
        /// Gets tags for context menu (right click) commands</summary>
        /// <param name="context">Context containing target object</param>
        /// <param name="target">Right clicked object, or null if none</param>
        /// <returns>Tags for context menu</returns>
        public virtual IEnumerable<object> GetCommands(object context, object target)
        {
            ControlInfo info = target as ControlInfo;
            if (info != null)
            {
                // if the Name property of the control looks like a file path, return
                //  the tab commands
                if (IsDocumentControl(info))
                {
                    return new object[]
                    {
                        Command.CloseCurrentTab,
                        Command.CloseOtherTabs,
                        Command.CopyFullPath,
                        Command.OpenContainingFolder,
                    };
                }
            }

            return EmptyEnumerable<object>.Instance;
        }

        #endregion

        #region ICommandClient Members

        /// <summary>
        /// Checks whether the client can do the command, if it handles it</summary>
        /// <param name="commandTag">Command to be done</param>
        /// <returns>True iff client can do the command</returns>
        public virtual bool CanDoCommand(object commandTag)
        {
            ControlInfo[] docInfos = GetDocumentControls();
            if (docInfos.Length > 0)
            {
                switch ((Command)commandTag)
                {
                    case Command.CloseCurrentTab:
                        return m_controlRegistry.ActiveControl.Group != StandardControlGroup.CenterPermanent;

                    case Command.CloseOtherTabs:
                        return docInfos.Length > 1;

                    case Command.CopyFullPath:
                    case Command.OpenContainingFolder:
                        return File.Exists(GetDocumentPath(m_controlRegistry.ActiveControl));
                }
            }

            return false;
        }

        /// <summary>
        /// Does the command</summary>
        /// <param name="commandTag">Command to be done</param>
        public virtual void DoCommand(object commandTag)
        {
            switch ((Command)commandTag)
            {
                case Command.CloseCurrentTab:
                    Close(m_controlRegistry.ActiveControl);
                    break;

                case Command.CloseOtherTabs:
                    CloseOthers(m_controlRegistry.ActiveControl);
                    break;

                case Command.CopyFullPath:
                    Clipboard.SetDataObject(m_controlRegistry.ActiveControl.Description, true);
                    break;

                case Command.OpenContainingFolder:
                    // Open in Explorer and select the file. http://support.microsoft.com/kb/314853
                    System.Diagnostics.Process.Start("explorer.exe", "/e,/select," + m_controlRegistry.ActiveControl.Description);
                    break;
            }

        }

        /// <summary>
        /// Updates command state for given command</summary>
        /// <param name="commandTag">Command</param>
        /// <param name="commandState">Command state to update</param>
        void ICommandClient.UpdateCommand(object commandTag, CommandState commandState)
        {
        }

        #endregion

        private void Close(ControlInfo info)
        {
            if (info.Client.Close(info.Control))
            {
                m_controlHostService.UnregisterControl(info.Control);
            }
        }

        private void CloseOthers(ControlInfo info)
        {
            ControlInfo[] docInfos = GetDocumentControls();
            foreach (ControlInfo docInfo in docInfos)
                if (docInfo != info)
                    Close(docInfo);
        }

        // Gets info for all controls holding a document
        private ControlInfo[] GetDocumentControls()
        {
            List<ControlInfo> infos = new List<ControlInfo>();
            foreach (ControlInfo info in m_controlHostService.Controls)
                if (IsDocumentControl(info))
                    infos.Add(info);

            return infos.ToArray();
        }

        // Determines if control info represents a document
        private bool IsDocumentControl(ControlInfo info)
        {
            return info.Client.Is<IDocumentClient>();

            //if (info.Client.Is<IDocumentClient>())
            //    return true;
            // Path.HasExtension(name) is not enought to determine if control is
            // the control is DocumentControl
            //string name = info.Name;
            //return
            //    !string.IsNullOrEmpty(name) &&
            //    Path.HasExtension(name);
        }

        // Gets the document's full path name, trimmed of invalid characters, like the '*' if the document
        //  was modified. Assumes that 'info' represents a document.
        private string GetDocumentPath(ControlInfo info)
        {
            string path = info.Description;
            path = path.Trim(s_invalidFileNameChars);
            return path;
        }

        protected ICommandService CommandService
        {
            get { return m_commandService; }
        }

        private readonly ICommandService m_commandService;
        private readonly IControlHostService m_controlHostService;
        private readonly IControlRegistry m_controlRegistry;
        private static readonly char[] s_invalidFileNameChars = Path.GetInvalidFileNameChars();
    }
}
