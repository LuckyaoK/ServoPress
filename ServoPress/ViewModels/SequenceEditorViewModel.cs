using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ServoPress.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServoPress.ViewModels
{
    public partial class SequenceEditorViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<SequenceStep> _sequenceSteps;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(DeleteStepCommand))]
        [NotifyCanExecuteChangedFor(nameof(MoveStepUpCommand))]
        [NotifyCanExecuteChangedFor(nameof(MoveStepDownCommand))]
        private SequenceStep  _selectedStep;

        public SequenceEditorViewModel()
        {
            _sequenceSteps = new ObservableCollection<SequenceStep>
            {
              
                new LabelStep{ StepNumber = 1, LabelId = 1 },
                new MeasureStep { StepNumber = 2, Mode = MeasureStep.MeasureMode.Start },
                new MotionStep { StepNumber = 3, Position = 62.00, MaxForce = 1000 },
                new MotionStep { StepNumber = 4, Position = 68.00, MaxForce = 400 },
                new TimerStep { StepNumber = 5, Duration = 500 },
                new MeasureStep { StepNumber = 6, Mode = MeasureStep.MeasureMode.Stop },
                new HomePositionStep { StepNumber = 6 }
            };


            // 默认选中第一项
            _selectedStep = _sequenceSteps[0];
        }

        #region 添加步骤命令
        [RelayCommand] private void AddMotionStep() => AddStep(new MotionStep());
        [RelayCommand] private void AddMeasureStep() => AddStep(new MeasureStep());
        [RelayCommand] private void AddTimeStep() => AddStep(new TimerStep());
        [RelayCommand] private void AddWaitStep() => AddStep(new WaitStep());
        [RelayCommand] private void AddLabelStep() => AddStep(new LabelStep());
        [RelayCommand] private void AddJumpStep() => AddStep(new JumpStep());
        [RelayCommand] private void AddHomeStep() => AddStep(new HomePositionStep());
        [RelayCommand] private void AddEndStep() => AddStep(new EndStep());

        private void AddStep(SequenceStep step)
        {
            SequenceSteps.Add(step);
            RenumberSteps();
            SelectedStep = step; // 添加后自动选中
        }
        #endregion

        #region 操作步骤

        private bool CanExcute() => SelectedStep != null && SequenceSteps.IndexOf(SelectedStep) > 0;

        [RelayCommand(CanExecute = "CanExcute")]
        private void MoveStepUp()
        {
            if (SelectedStep == null) return;
            int index = SequenceSteps.IndexOf(SelectedStep);
            if (index > 0)
            {
                SequenceSteps.Move(index, index - 1);
                RenumberSteps();
            }
        }

        [RelayCommand(CanExecute = "CanExcute")]
        private void MoveStepDown()
        {
            if (SelectedStep == null) return;
            int index = SequenceSteps.IndexOf(SelectedStep);
            if (index < SequenceSteps.Count - 1)
            {
                SequenceSteps.Move(index, index + 1);
                RenumberSteps();
            }
        }

        [RelayCommand(CanExecute = "CanExcute")]
        private void DeleteStep()
        {
            SequenceSteps.Remove(SelectedStep);
            RenumberSteps();
        }


        #endregion

        [RelayCommand]
        private void SaveSequence()
        {
            // 在这里添加保存整个序列的逻辑
            // (例如, 序列化 _sequenceSteps 列表到 JSON 或 XML)
        }

        /// <summary>
        /// 重新为列表中的所有步骤编号
        /// </summary>
        private void RenumberSteps()
        {
            for (int i = 0; i < SequenceSteps.Count; i++)
            {
                SequenceSteps[i].StepNumber = i + 1;
            }
        }
    }
}
