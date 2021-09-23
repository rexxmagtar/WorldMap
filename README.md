# WorldMap

##Общее описание
Карта мира используется игроком для выбора игрового уровня.
Карта состоит из различных фрагментов следующих друг за другом. С целью экономии памяти фрагменты выгружаются из оперативной памяти при уходе их из зоны видимости пользователя.
Так же с целью уменьшения начального размера приложения, все фрагменты карты хранятся первоначально на сервере и загружаются на устройство пользователя только при попадании в область видимости пользователя после чего с устройства уже не удаляются. Для этого использовались Addressables.

##Фрагменты карты
Каждый фрагмент карты содержит кнопки перехода на уровни и выполняет логику инициализации, активации и деакции всех своих компонентов.

![image](https://user-images.githubusercontent.com/51932532/134591547-17414e95-27af-4d51-8956-c99dd43f32b1.png)


##Примеры работы:
https://user-images.githubusercontent.com/51932532/134588688-ce498942-84de-4947-ad2b-04c54ead07dd.MP4

https://user-images.githubusercontent.com/51932532/134588141-dafaa79c-ec59-42f1-a879-42c9faaf9611.mp4




