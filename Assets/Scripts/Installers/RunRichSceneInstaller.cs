using RunRich3D.Common;
using RunRich3D.GameEngine;
using ButchersGames;
using UnityEngine;
using Zenject;

namespace RunRich3D.Installers
{
    public sealed class RunRichSceneInstaller : MonoInstaller
    {
        [SerializeField] private LevelManager levelManager;

        public override void InstallBindings()
        {
            levelManager.Init();

            Container.Bind<LevelManager>().FromInstance(levelManager).AsSingle();
            Container.Bind<IPlayerProgressService>().To<PlayerProgressService>().AsSingle();
            Container.Bind<RunRichGameController>().AsSingle();
        }
    }
}
