import pandas as pd
from sklearn.ensemble import RandomForestClassifier
from sklearn.model_selection import train_test_split
from sklearn.metrics import classification_report, accuracy_score
import joblib

# Cargar dataset
df = pd.read_csv("dataset_dificultad_adaptativa.csv")

X = df[[
    "zona",
    "senales_mostradas",
    "aciertos",
    "errores",
    "tiempo_promedio"
]]

y = df["dificultad_siguiente"]

X_train, X_test, y_train, y_test = train_test_split(
    X, y,
    test_size=0.2,
    random_state=42
)

model = RandomForestClassifier(
    n_estimators=120,
    max_depth=7,
    random_state=42
)

model.fit(X_train, y_train)

y_pred = model.predict(X_test)

print("Accuracy:", accuracy_score(y_test, y_pred))
print("\nReporte:")
print(classification_report(y_test, y_pred))

# Importancia de variables
importances = pd.Series(
    model.feature_importances_,
    index=X.columns
).sort_values(ascending=False)

print("\nImportancia de variables:")
print(importances)

# Guardar modelo entrenado
joblib.dump(model, "modelo_dificultad.pkl")
print("\nModelo guardado en 'modelo_dificultad.pkl'")
