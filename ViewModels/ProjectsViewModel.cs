using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using TimeTracker.Models;
using TimeTracker.Services;

namespace TimeTracker.ViewModels
{
    /// <summary>Окно управления проектами и правилами классификации.</summary>
    public class ProjectsViewModel : INotifyPropertyChanged
    {
        private readonly ProjectRepository _repo;

        public ObservableCollection<Project> Projects { get; } = new();
        public ObservableCollection<ClassificationRule> SelectedProjectRules { get; } = new();
        public ObservableCollection<ProjectNotification> SelectedProjectNotifications { get; } = new();

        public RuleTypeItem[] RuleTypes => RuleTypeItem.All;
        public NotificationTypeItem[] NotificationTypes => NotificationTypeItem.All;
        public SeverityItem[] SeverityLevels => SeverityItem.All;

        /// <summary>Сработало любое изменение проектов/правил — MainViewModel должен перечитать их.</summary>
        public event EventHandler? Changed;

        private Project? _selectedProject;
        public Project? SelectedProject
        {
            get => _selectedProject;
            set
            {
                _selectedProject = value;
                OnPropertyChanged();
                RefreshRules();
                RefreshNotifications();
            }
        }

        private ClassificationRule? _selectedRule;
        public ClassificationRule? SelectedRule
        {
            get => _selectedRule;
            set { _selectedRule = value; OnPropertyChanged(); }
        }

        private ProjectNotification? _selectedNotification;
        public ProjectNotification? SelectedNotification
        {
            get => _selectedNotification;
            set { _selectedNotification = value; OnPropertyChanged(); }
        }

        public ICommand AddProjectCommand { get; }
        public ICommand DeleteProjectCommand { get; }
        public ICommand SaveProjectCommand { get; }
        public ICommand AddRuleCommand { get; }
        public ICommand DeleteRuleCommand { get; }
        public ICommand SaveRuleCommand { get; }
        public ICommand AddNotificationCommand { get; }
        public ICommand DeleteNotificationCommand { get; }
        public ICommand SaveNotificationCommand { get; }

        public ProjectsViewModel(ProjectRepository repo)
        {
            _repo = repo;

            AddProjectCommand = new RelayCommand(() =>
            {
                var project = new Project { Name = "Новый проект", Color = "#4A90D9" };
                project.Id = _repo.AddProject(project);
                RefreshProjects();
                SelectedProject = Projects.FirstOrDefault(p => p.Id == project.Id);
                Changed?.Invoke(this, EventArgs.Empty);
            });

            DeleteProjectCommand = new RelayCommand(() =>
            {
                if (SelectedProject == null)
                    return;
                _repo.DeleteProject(SelectedProject.Id);
                RefreshProjects();
                Changed?.Invoke(this, EventArgs.Empty);
            }, () => SelectedProject != null);

            SaveProjectCommand = new RelayCommand(() =>
            {
                if (SelectedProject == null || string.IsNullOrWhiteSpace(SelectedProject.Name))
                    return;
                _repo.UpdateProject(SelectedProject);
                RefreshProjects();
                Changed?.Invoke(this, EventArgs.Empty);
            }, () => SelectedProject != null);

            AddRuleCommand = new RelayCommand(() =>
            {
                if (SelectedProject == null)
                    return;
                _repo.AddRule(new ClassificationRule { ProjectId = SelectedProject.Id });
                RefreshRules();
                Changed?.Invoke(this, EventArgs.Empty);
            }, () => SelectedProject != null);

            DeleteRuleCommand = new RelayCommand(() =>
            {
                if (SelectedRule == null)
                    return;
                _repo.DeleteRule(SelectedRule.Id);
                RefreshRules();
                Changed?.Invoke(this, EventArgs.Empty);
            }, () => SelectedRule != null);

            SaveRuleCommand = new RelayCommand(() =>
            {
                if (SelectedRule == null || string.IsNullOrWhiteSpace(SelectedRule.Pattern))
                    return;
                _repo.UpdateRule(SelectedRule);
                RefreshRules();
                Changed?.Invoke(this, EventArgs.Empty);
            }, () => SelectedRule != null);

            AddNotificationCommand = new RelayCommand(() =>
            {
                if (SelectedProject == null)
                    return;
                var notification = new ProjectNotification { ProjectId = SelectedProject.Id };
                notification.Id = _repo.AddNotification(notification);
                RefreshNotifications();
                SelectedNotification = SelectedProjectNotifications.FirstOrDefault(n => n.Id == notification.Id);
                Changed?.Invoke(this, EventArgs.Empty);
            }, () => SelectedProject != null);

            DeleteNotificationCommand = new RelayCommand(() =>
            {
                if (SelectedNotification == null)
                    return;
                _repo.DeleteNotification(SelectedNotification.Id);
                RefreshNotifications();
                Changed?.Invoke(this, EventArgs.Empty);
            }, () => SelectedNotification != null);

            SaveNotificationCommand = new RelayCommand(() =>
            {
                if (SelectedNotification == null || SelectedNotification.ThresholdSeconds <= 0)
                    return;
                _repo.UpdateNotification(SelectedNotification);
                RefreshNotifications();
                Changed?.Invoke(this, EventArgs.Empty);
            }, () => SelectedNotification != null);

            RefreshProjects();
        }

        private void RefreshProjects()
        {
            var selectedId = SelectedProject?.Id;
            Projects.Clear();
            foreach (var project in _repo.GetProjects())
                Projects.Add(project);
            SelectedProject = Projects.FirstOrDefault(p => p.Id == selectedId) ?? Projects.FirstOrDefault();
        }

        private void RefreshRules()
        {
            SelectedProjectRules.Clear();
            if (SelectedProject == null)
                return;
            foreach (var rule in _repo.GetRules().Where(r => r.ProjectId == SelectedProject.Id))
                SelectedProjectRules.Add(rule);
        }

        private void RefreshNotifications()
        {
            var selectedId = SelectedNotification?.Id;
            SelectedProjectNotifications.Clear();
            if (SelectedProject == null)
                return;
            foreach (var notification in _repo.GetNotifications().Where(n => n.ProjectId == SelectedProject.Id))
                SelectedProjectNotifications.Add(notification);
            SelectedNotification = SelectedProjectNotifications.FirstOrDefault(n => n.Id == selectedId)
                ?? SelectedProjectNotifications.FirstOrDefault();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
