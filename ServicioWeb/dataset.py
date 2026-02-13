import random
import pandas as pd

random.seed(42)
NUM_MUESTRAS = 2000
data = []

for _ in range(NUM_MUESTRAS):

    zona = random.randint(1, 4)

    senales_mostradas = random.randint(1, 10)
    aciertos = random.randint(0, senales_mostradas)
    errores = senales_mostradas - aciertos

    tiempo_promedio = round(random.uniform(1.5, 10), 2)

    # Lógica para definir dificultad siguiente
    if aciertos >= senales_mostradas * 0.8 and tiempo_promedio < 4:
        dificultad_siguiente = 2  # Alta
    elif aciertos >= senales_mostradas * 0.5:
        dificultad_siguiente = 1  # Media
    else:
        dificultad_siguiente = 0  # Baja

    data.append([
        zona,
        senales_mostradas,
        aciertos,
        errores,
        tiempo_promedio,
        dificultad_siguiente
    ])

df = pd.DataFrame(data, columns=[
    "zona",
    "senales_mostradas",
    "aciertos",
    "errores",
    "tiempo_promedio",
    "dificultad_siguiente"
])

df.to_csv("dataset_dificultad_adaptativa.csv", index=False)

print("Dataset generado")
print(df.head())
