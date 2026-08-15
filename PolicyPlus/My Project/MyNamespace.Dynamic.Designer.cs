using System;
using System.ComponentModel;
using System.Diagnostics;

namespace PolicyPlus.My
{
    internal static partial class MyProject
    {
        internal partial class MyForms
        {

            [EditorBrowsable(EditorBrowsableState.Never)]
            public EditPolNumericData m_EditPolNumericData;

            public EditPolNumericData EditPolNumericData
            {
                [DebuggerHidden]
                get
                {
                    m_EditPolNumericData = Create__Instance__(m_EditPolNumericData);
                    return m_EditPolNumericData;
                }
                [DebuggerHidden]
                set
                {
                    if (ReferenceEquals(value, m_EditPolNumericData))
                        return;
                    if (value is not null)
                        throw new ArgumentException("Property can only be set to Nothing");
                    Dispose__Instance__(ref m_EditPolNumericData);
                }
            }


            [EditorBrowsable(EditorBrowsableState.Never)]
            public EditSetting m_EditSetting;

            public EditSetting EditSetting
            {
                [DebuggerHidden]
                get
                {
                    m_EditSetting = Create__Instance__(m_EditSetting);
                    return m_EditSetting;
                }
                [DebuggerHidden]
                set
                {
                    if (ReferenceEquals(value, m_EditSetting))
                        return;
                    if (value is not null)
                        throw new ArgumentException("Property can only be set to Nothing");
                    Dispose__Instance__(ref m_EditSetting);
                }
            }


            [EditorBrowsable(EditorBrowsableState.Never)]
            public ListEditor m_ListEditor;

            public ListEditor ListEditor
            {
                [DebuggerHidden]
                get
                {
                    m_ListEditor = Create__Instance__(m_ListEditor);
                    return m_ListEditor;
                }
                [DebuggerHidden]
                set
                {
                    if (ReferenceEquals(value, m_ListEditor))
                        return;
                    if (value is not null)
                        throw new ArgumentException("Property can only be set to Nothing");
                    Dispose__Instance__(ref m_ListEditor);
                }
            }


            [EditorBrowsable(EditorBrowsableState.Never)]
            public Main m_Main;

            public Main Main
            {
                [DebuggerHidden]
                get
                {
                    m_Main = Create__Instance__(m_Main);
                    return m_Main;
                }
                [DebuggerHidden]
                set
                {
                    if (ReferenceEquals(value, m_Main))
                        return;
                    if (value is not null)
                        throw new ArgumentException("Property can only be set to Nothing");
                    Dispose__Instance__(ref m_Main);
                }
            }

        }


    }
}