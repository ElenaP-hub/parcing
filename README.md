# parcing
parcing web-source
В файле NewsRecordsController.Download описан код, с помощью которого с указанного веб-ресурса
обрабатывается и сохраняется в базу данных текстовая информация и картинки.
Веб-сайт представляет собой новостной портал на главной странице которого находится список новостей. При переходе по ссылке
конкретной новости переходим на новую страницу с контентом новости, котрый парсится, обрабатывается и сохраняется в базу данных. Перед этим 
происходит проверка на то, существует ли такой объект в базе данных или нет.
Для каждой новости создается объект с полями заголовок, дата публикации, текст товости, картинка, а также присваетвается
Id новости и Id изображения. 
