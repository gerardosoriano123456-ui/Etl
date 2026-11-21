import csv
import json
import os
import sqlite3
from contextlib import closing
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

ROOT = Path(__file__).resolve().parent


def read_csv(name):
    with (ROOT / name).open(encoding="utf-8-sig", newline="") as source:
        return list(csv.DictReader(source))


def initialize():
    opinions = {record["id_opinion"]: record for record in read_csv("opinion.csv")}
    reviews = read_csv("resena_web.csv")
    temporary = ROOT / "resenas-temporal.db"
    temporary.unlink(missing_ok=True)
    with closing(sqlite3.connect(temporary)) as db, db:
        db.executescript("""
            PRAGMA foreign_keys = ON;
            CREATE TABLE opinion (
                id_opinion INTEGER PRIMARY KEY, id_producto INTEGER NOT NULL,
                fecha_opinion TEXT NOT NULL, texto TEXT NOT NULL, calificacion INTEGER
            );
            CREATE TABLE resena_web (
                id_opinion INTEGER PRIMARY KEY REFERENCES opinion(id_opinion),
                sitio_web TEXT NOT NULL
            );
        """)
        db.executemany("""
            INSERT INTO opinion VALUES (:id_opinion,:id_producto,:fecha_opinion,:texto,:calificacion)
        """, (opinions[entry["id_opinion"]] for entry in reviews))
        db.executemany("INSERT INTO resena_web VALUES (:id_opinion,:sitio_web)", reviews)
    os.replace(temporary, ROOT / "resenas.db")
    return [
        {"id": row["id_opinion"], "plataforma": row["plataforma"],
         "texto": opinions[row["id_opinion"]]["texto"],
         "likes": int(row["cantidad_likes"])}
        for row in read_csv("comentario_social.csv")
    ]


class SocialHandler(BaseHTTPRequestHandler):
    comments = []

    def do_GET(self):
        if self.path != "/comentarios":
            self.send_error(404)
            return
        content = json.dumps(self.comments, ensure_ascii=False).encode()
        self.send_response(200)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(content)))
        self.end_headers()
        self.wfile.write(content)


if __name__ == "__main__":
    with ThreadingHTTPServer(("127.0.0.1", 8792), SocialHandler) as service:
        SocialHandler.comments = initialize()
        print("API en http://127.0.0.1:8792/comentarios", flush=True)
        service.serve_forever()
