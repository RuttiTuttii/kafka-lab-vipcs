# user service

владелец: Алиса, Даша

> ограничения: состояние хранится в файле `data/users.json` рядом с dll, возвратов и снятия резервов нет. событие уходит в брокер после изменения состояния: если брокер упадёт посередине, состояние и поток событий могут разойтись.

## запуск

```bash
# visual studio: открой Users.sln, выбери профиль "college broker" или "local broker" и жми F5

# одной командой на windows
powershell -ExecutionPolicy Bypass -File scripts\run-service.ps1 -Name user

# на ubuntu и macos
./scripts/run-service.sh user
```

* **порт:** 5003
* **читает:** `order.created2` (считает заказы пользователя)
* **пишет:** `user.registered2`

## API

| метод | путь | описание |
|---|---|---|
| POST | `/users` | регистрация `{"name","email"}`, публикует `user.registered2`, 409 при повторном email |
| GET | `/users`, `/users/{id}` | просмотр |
| PUT | `/users/{id}` | изменение данных `{"name","email"}` |
| GET | `/health` | проверка |

при первом запуске создаёт двух тестовых пользователей с фиксированными id из `contracts/events.md`.

контракт событий: [contracts/events.md](../../contracts/events.md)
