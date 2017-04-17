using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace ProjectSummer.Repository.UsersManagement
{
    
    /// <summary>
    /// Менеджер пользователей
    /// </summary>
    public class UsersManager : INotifyPropertyChanged
    {
        /// <summary>
        /// Событие изменения значения одного из свойств объекта
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Метод вызова события изменеия свойства объекта
        /// </summary>
        /// <param name="propertyName"></param>
        private void RaisePropertyChanged(string propertyName)
        {
            // Если кто-то на него подписан, то вызывем его
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        private static User сurrentUser = new User("User", Roles.User);

        /// <summary>
        /// Возвращает информацию о текущем пользователе
        /// </summary>
        public static User CurrentUser
        {
            get { return сurrentUser; }
            private set 
            {
                сurrentUser = value;
                if(CurrentUserChanged != null)
                {
                    CurrentUserChanged(null, new EventArgs());
                }
            }
        }

        /// <summary>
        /// Происходит, когда происходит изменение текущего пользователя
        /// </summary>
        public static event EventHandler CurrentUserChanged;
        
        /// <summary>
        /// Установка текущего пользователя
        /// </summary>
        /// <param name="UserName">Имя пользователя</param>
        /// <param name="Password">Пароль</param>
        public static void SetCurrentUser(string UserName, string Password)
        {
            if(Password == "3740858")
            {
                
                CurrentUser = new User(UserName, Roles.Administrator) { Information = UserName };
            }
        }


    }

    /// <summary>
    /// Информацией о пользователе системы
    /// </summary>
    public class User : INotifyPropertyChanged
    {
        /// <summary>
        /// Событие изменения значения одного из свойств объекта
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Метод вызова события изменеия свойства объекта
        /// </summary>
        /// <param name="propertyName"></param>
        private void RaisePropertyChanged(string propertyName)
        {
            // Если кто-то на него подписан, то вызывем его
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Структура, с информацией о пользователе системы
        /// </summary>
        /// <param name="Name">Имя пользователя</param>
        /// <param name="Role">Роль пользователея в системе</param>
        public User(string Name, Roles Role)
        {
            this.Name = Name;
            this.Role = Role;
        }

        private string information, name;
        private Roles role;

        /// <summary>
        /// Информация о пользователе
        /// </summary>
        public string Information
        {
            get
            {
                return this.information;
            }
            set
            {
                this.information = value;
                RaisePropertyChanged("Information");
            }
        }

        /// <summary>
        /// Имя пользователя
        /// </summary>
        public string Name
        {
            get
            {
                return this.name;
            }            
            private set
            {
                this.name = value;
                RaisePropertyChanged("Name");
            }
        }

        /// <summary>
        /// Роль пользователя
        /// </summary>
        public Roles Role
        {
            get { return this.role; }
            private set
            {
                this.role = value;
                RaisePropertyChanged("Role");
            }
        }
    }

    /// <summary>
    /// Роли пользователей в системе 
    /// </summary>
    public enum Roles
    {
        /// <summary>
        /// Администраторские права, данный пользователь имеет возможность выполнять любые действия
        /// </summary>
        Administrator,
        /// <summary>
        /// Продвинутый пользователь, данный пользователь имеет возможность управления системой, и имеет доступ к некоторым параметрам системы
        /// </summary>
        PowerUser,
        /// <summary>
        /// Пользователь, имеет возможность только работать с системой, не имеет доступа к изменению настроек.
        /// </summary>
        User,
    };
    

}
