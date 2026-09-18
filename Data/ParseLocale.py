import re
import requests


def parse_locales(file_data):
    locales = []
    for locale_name, locale_var in re.findall(
        r'{title:"([^"]+)",locale:([^}]+)}', file_data.decode("utf-8")
    ):
        locale_id = f"{locale_var[2:4]}-{locale_var[4:]}"
        locales.append((locale_id, locale_name))
    return locales


def main():
    url = "https://assets-www.xbox.com/xbox-web/static/js/LocalePickerPage.7c45fcf5.chunk.js"
    resp = requests.get(url, timeout=60)
    resp.raise_for_status()
    for locale_id, locale_name in parse_locales(resp.content):
        print(f'ret.Add(new XboxLocale("{locale_id}", "{locale_name}"));')


if __name__ == "__main__":
    main()
