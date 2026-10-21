using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace grapher.CurveEditing
{
    public sealed class UserPresetsForm : StudioForm
    {
        public UserCurvePreset SelectedPreset { get; private set; }
        public UserPresetsForm(UserPresetStore store, Func<UserCurvePreset> capture)
        {
            SuspendLayout(); Text="我的预设"; ClientSize=new Size(620,540); AutoScaleMode=AutoScaleMode.None;
            Resizable=false; MaximizeBox=false; MinimizeBox=false; StartPosition=FormStartPosition.CenterParent;
            Font=new Font(LightTheme.FontFamily,10); BackColor=LightTheme.Page; ForeColor=LightTheme.Ink;
            ModernWindow.Attach(this);
            var layout=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(24),ColumnCount=1,RowCount=6};
            foreach(int h in new[]{44,48,0,64,36,48}) layout.RowStyles.Add(h==0 ? new RowStyle(SizeType.Percent,100):new RowStyle(SizeType.Absolute,h));
            layout.Controls.Add(new Label {Text="我的预设",Font=new Font(Font.FontFamily,20,FontStyle.Bold),Dock=DockStyle.Fill,Margin=Padding.Empty});
            layout.Controls.Add(new Label {Text="保存当前预览，随时找回熟悉的手感。\n载入只改变预览，点击主界面的「应用曲线」后才生效。",Dock=DockStyle.Fill,ForeColor=LightTheme.Secondary,Margin=Padding.Empty});
            var list=new ListBox {Name="SavedPresets",Dock=DockStyle.Fill,DisplayMember="Name",BorderStyle=BorderStyle.None,BackColor=LightTheme.Surface,ForeColor=LightTheme.Ink,IntegralHeight=false};
            var card=new RoundedCard {Dock=DockStyle.Fill,Padding=new Padding(16),Margin=new Padding(0,8,0,8),ClipChildren=true}; card.Controls.Add(list); layout.Controls.Add(card);
            var name=new TextBox {Name="PresetName",Width=370,MaxLength=40,BorderStyle=BorderStyle.None,BackColor=LightTheme.Surface,Font=Font,Margin=Padding.Empty};
            var nameRow=new AlignedControlRow {Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty};
            nameRow.Controls.Add(new Label {Text="名称",AutoSize=true,Margin=new Padding(0,0,16,0)});
            var field=new RoundedCard {Width=410,Height=40,Padding=new Padding(12,0,12,0),ClipChildren=true,Margin=Padding.Empty};
            var fieldRow=new AlignedControlRow {Dock=DockStyle.Fill,WrapContents=false,BackColor=LightTheme.Surface};fieldRow.Controls.Add(name);field.Controls.Add(fieldRow);nameRow.Controls.Add(field);layout.Controls.Add(nameRow);
            var message=new Label {Dock=DockStyle.Fill,ForeColor=LightTheme.Secondary,TextAlign=ContentAlignment.MiddleLeft,Margin=Padding.Empty};layout.Controls.Add(message);
            var actions=new AlignedControlRow {Dock=DockStyle.Fill,WrapContents=false,FlowDirection=FlowDirection.RightToLeft,Margin=Padding.Empty};
            Func<string,int,Button> button=(text,width)=>new StudioButton {Text=text,Width=width,Height=40,BackColor=LightTheme.SurfaceAlt,ForeColor=LightTheme.Ink,FlatStyle=FlatStyle.Flat,Margin=new Padding(8,0,0,0),Cursor=Cursors.Hand};
            var load=button("载入预览",104); load.Name="LoadUserPreset"; load.BackColor=LightTheme.Accent; load.ForeColor=LightTheme.AccentInk;
            var save=button("保存当前",104);save.Name="SaveUserPreset";var delete=button("删除",64);var undo=button("撤回删除",96);var close=button("关闭",64);
            close.DialogResult=DialogResult.Cancel;close.Click+=(s,e)=>Close();CancelButton=close;
            actions.Controls.AddRange(new Control[]{load,save,delete,undo,close});layout.Controls.Add(actions);Controls.Add(layout);
            UserCurvePreset deleted=null;
            Action<string> refresh=id=>{
                list.BeginUpdate();list.Items.Clear();foreach(var preset in store.Items)list.Items.Add(preset);
                list.SelectedIndex=list.Items.Count==0 ? -1 : Math.Max(0,list.Items.Cast<UserCurvePreset>().ToList().FindIndex(p=>p.Id==id));list.EndUpdate();
                load.Enabled=delete.Enabled=list.SelectedItem!=null;undo.Enabled=deleted!=null;
                if(list.Items.Count==0) message.Text="还没有预设：输入名称，点击「保存当前」。";
            };
            list.SelectedIndexChanged+=(s,e)=>{load.Enabled=delete.Enabled=list.SelectedItem!=null;if(list.SelectedItem is UserCurvePreset preset) name.Text=preset.Name;};
            Action<Action> attempt=action=>{try{action();message.ForeColor=LightTheme.Secondary;}catch(Exception ex){message.Text=ex.Message;message.ForeColor=LightTheme.Live;}};
            save.Click+=(s,e)=>attempt(()=>{var preset=capture();preset.Name=name.Text;var saved=store.Add(preset);refresh(saved.Id);message.Text="已保存「"+saved.Name+"」，当前鼠标手感保持不变。";});
            delete.Click+=(s,e)=>attempt(()=>{if(!(list.SelectedItem is UserCurvePreset preset))return;deleted=store.Delete(preset.Id);refresh(null);message.Text="已删除，可点击「撤回删除」恢复。";});
            undo.Click+=(s,e)=>attempt(()=>{if(deleted==null)return;var restored=deleted;store.Restore(restored);deleted=null;refresh(restored.Id);message.Text="已恢复预设。";});
            load.Click+=(s,e)=>{if(list.SelectedItem is UserCurvePreset preset){SelectedPreset=preset.Copy();DialogResult=DialogResult.OK;Close();}};
            refresh(null);
            using(var display=CreateGraphics())Scale(new SizeF(display.DpiX/96,display.DpiY/96)); ResumeLayout(true);
        }
    }
}
